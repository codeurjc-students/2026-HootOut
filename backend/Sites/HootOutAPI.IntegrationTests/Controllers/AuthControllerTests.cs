using Autofac;
using FluentAssertions;
using HootOut.CommonDomain.DefaultValues;
using HootOut.CommonDomain.Persistence;
using HootOut.CommonIntegrationTests.PostgreSQL;
using HootOut.Contracts.Authentication.Requests;
using HootOut.Contracts.Authentication.Responses;
using HootOut.HootOutAPI.IntegrationTests.Common;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace HootOut.HootOutAPI.IntegrationTests.Controllers
{
    [Collection("IntegrationTests")]
    public class AuthControllerTests : TestControllerBase, IClassFixture<APIFixture>, IDisposable, IAsyncLifetime
    {
        private readonly ILifetimeScope container;
        private ClearAllTables clearTables;

        public AuthControllerTests(APIFixture apiFixture)
        {
            httpClient = apiFixture.CreateClient();
            container = apiFixture.AutofacRoot.BeginLifetimeScope();

            var testPersistenceProvider = container.Resolve<IPersistenceProvider>();
            Assert.Same(apiFixture.PostgreSQLProvider, testPersistenceProvider);

            clearTables = container.Resolve<ClearAllTables>();
            var defaultValues = container.Resolve<IEnumerable<IDefaultValues>>().OrderBy(x => x.Priority);

            foreach (var value in defaultValues)
            {
                value.Init();
            }
        }

        public ValueTask InitializeAsync()
        {
            clearTables?.ClearTables();
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            clearTables?.ClearTables();
            return ValueTask.CompletedTask;
        }

        // ------------------------------------------------------------------
        // Login
        // ------------------------------------------------------------------

        [Fact]
        public async Task Login_ReturnsOk_WithTokens()
        {
            var ct = TestContext.Current.CancellationToken;
            await RegisterAsync(ct);

            var response = await LoginAsync(DefaultEmail, DefaultPassword, ct);

            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(ct);
            auth.Should().NotBeNull();
            auth!.AccessToken.Should().NotBeNullOrWhiteSpace();
            auth.RefreshToken.Should().NotBeNullOrWhiteSpace();
        }

        [Fact]
        public async Task Login_AccessToken_AllowsCallingProtectedEndpoint()
        {
            var ct = TestContext.Current.CancellationToken;
            await RegisterAsync(ct);

            var loginResponse = await LoginAsync(DefaultEmail, DefaultPassword, ct);
            var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>(ct);

            var response = await GetUserInfoRequestAsync(auth!.AccessToken, ct);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Login_ReturnsBadRequest_WrongPassword()
        {
            var ct = TestContext.Current.CancellationToken;
            await RegisterAsync(ct);

            var response = await LoginAsync(DefaultEmail, "not-the-password", ct);

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Login_ReturnsBadRequest_UnknownUser()
        {
            var ct = TestContext.Current.CancellationToken;

            var response = await LoginAsync("nobody", DefaultPassword, ct);

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Login_TwiceForSameUser_ReturnsDifferentRefreshTokens()
        {
            var ct = TestContext.Current.CancellationToken;
            await RegisterAsync(ct);

            var first = await LoginAndReadAsync(ct);
            var second = await LoginAndReadAsync(ct);

            second.RefreshToken.Should().NotBe(first.RefreshToken);
        }

        // ------------------------------------------------------------------
        // Refresh
        // ------------------------------------------------------------------

        [Fact]
        public async Task Refresh_ReturnsOk_WithNewTokens()
        {
            var ct = TestContext.Current.CancellationToken;
            var original = await RegisterAsync(ct);

            var response = await RefreshAsync(original.RefreshToken, ct);

            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var refreshed = await response.Content.ReadFromJsonAsync<AuthResponse>(ct);
            refreshed.Should().NotBeNull();
            refreshed!.AccessToken.Should().NotBeNullOrWhiteSpace();
            refreshed.RefreshToken.Should().NotBeNullOrWhiteSpace();
            refreshed.RefreshToken.Should().NotBe(original.RefreshToken, "refresh tokens are rotated on every use");
        }

        [Fact]
        public async Task Refresh_NewAccessToken_AllowsCallingProtectedEndpoint()
        {
            var ct = TestContext.Current.CancellationToken;
            var original = await RegisterAsync(ct);

            var refreshed = await RefreshAndReadAsync(original.RefreshToken, ct);
            var response = await GetUserInfoRequestAsync(refreshed.AccessToken, ct);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Refresh_ReturnsBadRequest_InvalidToken()
        {
            var ct = TestContext.Current.CancellationToken;

            var response = await RefreshAsync("this-is-not-a-real-refresh-token", ct);

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Refresh_ReturnsBadRequest_WhenOldTokenIsReused()
        {
            var ct = TestContext.Current.CancellationToken;
            var original = await RegisterAsync(ct);

            (await RefreshAsync(original.RefreshToken, ct)).StatusCode.Should().Be(HttpStatusCode.OK);

            // Refresh tokens are single use: the same one again must be rejected.
            var replay = await RefreshAsync(original.RefreshToken, ct);

            replay.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Refresh_ReuseOfOldToken_RevokesTheWholeSession()
        {
            var ct = TestContext.Current.CancellationToken;
            var tokenA = await RegisterAsync(ct);
            var tokenB = await RefreshAndReadAsync(tokenA.RefreshToken, ct);

            // Someone replays A (already used): the server should treat it as theft...
            (await RefreshAsync(tokenA.RefreshToken, ct)).StatusCode.Should().Be(HttpStatusCode.BadRequest);

            // ...and kill the newest token of that session too.
            var response = await RefreshAsync(tokenB.RefreshToken, ct);

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Refresh_ReuseInOneSession_DoesNotAffectAnotherSession()
        {
            var ct = TestContext.Current.CancellationToken;
            var session1 = await RegisterAsync(ct);        // session 1 (e.g. the browser)
            var session2 = await LoginAndReadAsync(ct);    // session 2 (e.g. the phone)

            (await RefreshAsync(session1.RefreshToken, ct)).StatusCode.Should().Be(HttpStatusCode.OK);
            (await RefreshAsync(session1.RefreshToken, ct)).StatusCode.Should().Be(HttpStatusCode.BadRequest);

            // Session 2 has its own family and must keep working.
            var response = await RefreshAsync(session2.RefreshToken, ct);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Refresh_ReturnsBadRequest_AfterLogout()
        {
            var ct = TestContext.Current.CancellationToken;
            var auth = await RegisterAsync(ct);

            var logout = await httpClient.PostAsJsonAsync(
                "/api/v1/auth/logout", new AuthRefreshRequest { RefreshToken = auth.RefreshToken }, ct);
            logout.IsSuccessStatusCode.Should().BeTrue();

            var response = await RefreshAsync(auth.RefreshToken, ct);

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------ 

        private Task<HttpResponseMessage> RefreshAsync(string refreshToken, CancellationToken ct) =>
            httpClient.PostAsJsonAsync(
                "/api/v1/auth/refresh",
                new AuthRefreshRequest { RefreshToken = refreshToken },
                ct);

        private async Task<AuthResponse> RefreshAndReadAsync(string refreshToken, CancellationToken ct)
        {
            var response = await RefreshAsync(refreshToken, ct);
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(ct);
            auth.Should().NotBeNull();
            return auth!;
        }

        private async Task<HttpResponseMessage> GetUserInfoRequestAsync(string accessToken, CancellationToken ct)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/users/me");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            return await httpClient.SendAsync(request, ct);
        }

        public void Dispose()
        {
            container.Dispose();
        }
    }
}