using Autofac;
using FluentAssertions;
using HootOut.CommonDomain.DefaultValues;
using HootOut.CommonDomain.Persistence;
using HootOut.CommonIntegrationTests.PostgreSQL;
using HootOut.Contracts.Authentication.Requests;
using HootOut.Contracts.Authentication.Responses;
using HootOut.Contracts.Users.Dtos.Request;
using HootOut.HootOutAPI.IntegrationTests.Common;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace HootOut.HootOutAPI.IntegrationTests.Controllers
{
    /// <summary>
    /// Tests for the cookie delivery mode of the refresh token ("X-Token-Delivery: cookie").
    ///
    /// The client does NOT handle cookies: each test reads the Set-Cookie header and sends the
    /// Cookie header by hand. That avoids the test HttpClient dropping Secure cookies over http://localhost
    /// and lets the tests replay old cookie values on purpose.
    /// </summary>
    public class AuthCookieControllerTests : IClassFixture<APIFixture>, IDisposable, IAsyncLifetime
    {
        private const string CookieName = "refresh_token";
        private const string DeliveryHeader = "X-Token-Delivery";
        private const string Email = "test@test.com";
        private const string Password = "test1";

        private readonly ILifetimeScope container;
        private readonly HttpClient httpClient;
        private ClearAllTables clearTables;

        public AuthCookieControllerTests(APIFixture apiFixture)
        {
            httpClient = apiFixture.CreateHttpsClient();
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
        // Login / register
        // ------------------------------------------------------------------

        [Fact]
        public async Task Register_CookieMode_SetsHardenedCookie_AndOmitsTokenFromBody()
        {
            var ct = TestContext.Current.CancellationToken;

            var response = await RegisterAsync(cookieMode: true, ct);

            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var setCookie = GetRefreshSetCookie(response);
            setCookie.Should().NotBeNull("cookie mode must set the refresh cookie");
            setCookie!.Should().ContainEquivalentOf("httponly");
            setCookie.Should().ContainEquivalentOf("secure");
            setCookie.Should().ContainEquivalentOf("samesite=strict");
            setCookie.Should().ContainEquivalentOf("path=/api/v1/auth");
            GetCookieValue(response).Should().NotBeNullOrWhiteSpace();

            var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(ct);
            auth.Should().NotBeNull();
            auth!.AccessToken.Should().NotBeNullOrWhiteSpace();
            auth.RefreshToken.Should().BeNullOrEmpty("the refresh token must never reach page JavaScript");
        }

        [Fact]
        public async Task Login_CookieMode_SetsCookie_AndOmitsTokenFromBody()
        {
            var ct = TestContext.Current.CancellationToken;
            (await RegisterAsync(cookieMode: false, ct)).IsSuccessStatusCode.Should().BeTrue();

            var response = await LoginAsync(cookieMode: true, ct);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            GetCookieValue(response).Should().NotBeNullOrWhiteSpace();

            var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(ct);
            auth!.AccessToken.Should().NotBeNullOrWhiteSpace();
            auth.RefreshToken.Should().BeNullOrEmpty();
        }

        [Fact]
        public async Task Login_BodyMode_ReturnsTokenInBody_AndSetsNoCookie()
        {
            var ct = TestContext.Current.CancellationToken;
            (await RegisterAsync(cookieMode: false, ct)).IsSuccessStatusCode.Should().BeTrue();

            var response = await LoginAsync(cookieMode: false, ct);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            GetRefreshSetCookie(response).Should().BeNull("mobile and third-party clients keep using the body");

            var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(ct);
            auth!.RefreshToken.Should().NotBeNullOrWhiteSpace();
        }

        // ------------------------------------------------------------------
        // Refresh
        // ------------------------------------------------------------------

        [Fact]
        public async Task Refresh_CookieMode_RotatesCookie_AndReturnsWorkingAccessToken()
        {
            var ct = TestContext.Current.CancellationToken;
            var registered = await RegisterAsync(cookieMode: true, ct);
            var firstCookie = GetCookieValue(registered)!;

            var response = await RefreshAsync(firstCookie, cookieMode: true, ct);

            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var secondCookie = GetCookieValue(response);
            secondCookie.Should().NotBeNullOrWhiteSpace();
            secondCookie.Should().NotBe(firstCookie, "refresh tokens are rotated on every use");

            var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(ct);
            auth!.AccessToken.Should().NotBeNullOrWhiteSpace();
            auth.RefreshToken.Should().BeNullOrEmpty();

            (await GetMeStatusAsync(auth.AccessToken, ct)).Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Refresh_CookiePresent_ButNoDeliveryHeader_ReturnsBadRequest()
        {
            var ct = TestContext.Current.CancellationToken;
            var cookie = GetCookieValue(await RegisterAsync(cookieMode: true, ct))!;

            // Without the custom header the server ignores the cookie (this is what blocks cross-site forged requests).
            var response = await RefreshAsync(cookie, cookieMode: false, ct);

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Refresh_CookieMode_WithoutCookie_ReturnsBadRequest()
        {
            var ct = TestContext.Current.CancellationToken;

            var response = await RefreshAsync(refreshCookie: null, cookieMode: true, ct);

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Refresh_CookieMode_ReusedCookie_ReturnsBadRequest_ClearsCookie_AndRevokesSession()
        {
            var ct = TestContext.Current.CancellationToken;
            var cookieA = GetCookieValue(await RegisterAsync(cookieMode: true, ct))!;

            var refreshed = await RefreshAsync(cookieA, cookieMode: true, ct);
            var cookieB = GetCookieValue(refreshed)!;

            // Replaying the already-used cookie A is treated as theft.
            var replay = await RefreshAsync(cookieA, cookieMode: true, ct);

            replay.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            IsCookieCleared(replay).Should().BeTrue("a rejected cookie should be removed from the browser");

            // ...and the whole session dies, including the newest cookie.
            var afterReplay = await RefreshAsync(cookieB, cookieMode: true, ct);

            afterReplay.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        // ------------------------------------------------------------------
        // Logout
        // ------------------------------------------------------------------

        [Fact]
        public async Task Logout_CookieMode_ClearsCookie_AndRevokesSession()
        {
            var ct = TestContext.Current.CancellationToken;
            var cookie = GetCookieValue(await RegisterAsync(cookieMode: true, ct))!;

            var logout = await PostAsync("/api/v1/auth/logout", body: null, ct, cookieMode: true, refreshCookie: cookie);

            logout.IsSuccessStatusCode.Should().BeTrue();
            IsCookieCleared(logout).Should().BeTrue();

            // The cookie value is dead on the server even if someone kept a copy.
            var refresh = await RefreshAsync(cookie, cookieMode: true, ct);

            refresh.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private Task<HttpResponseMessage> RegisterAsync(bool cookieMode, CancellationToken ct) =>
            PostAsync(
                "/api/v1/auth/register",
                new CreateUserRequest { Username = "test1", Email = Email, Password = Password },
                ct,
                cookieMode);

        private Task<HttpResponseMessage> LoginAsync(bool cookieMode, CancellationToken ct) =>
            PostAsync(
                "/api/v1/auth/login",
                new AuthRequest { Email = Email, Password = Password },
                ct,
                cookieMode);

        // Cookie mode sends no body: the refresh token comes from the Cookie header.
        private Task<HttpResponseMessage> RefreshAsync(string? refreshCookie, bool cookieMode, CancellationToken ct) =>
            PostAsync("/api/v1/auth/refresh", body: null, ct, cookieMode, refreshCookie);

        private async Task<HttpResponseMessage> PostAsync(
            string url, object? body, CancellationToken ct, bool cookieMode = false, string? refreshCookie = null)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url);

            if (body is not null)
                request.Content = JsonContent.Create(body);

            if (cookieMode)
                request.Headers.Add(DeliveryHeader, "cookie");

            if (refreshCookie is not null)
                request.Headers.Add("Cookie", $"{CookieName}={refreshCookie}");

            return await httpClient.SendAsync(request, ct);
        }

        private async Task<HttpStatusCode> GetMeStatusAsync(string accessToken, CancellationToken ct)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/users/me");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            var response = await httpClient.SendAsync(request, ct);
            return response.StatusCode;
        }

        private static string? GetRefreshSetCookie(HttpResponseMessage response) =>
            response.Headers.TryGetValues("Set-Cookie", out var values)
                ? values.FirstOrDefault(v => v.StartsWith(CookieName + "=", StringComparison.Ordinal))
                : null;

        private static string? GetCookieValue(HttpResponseMessage response) =>
            GetRefreshSetCookie(response)?.Split(';')[0][(CookieName.Length + 1)..];

        // ASP.NET Core removes a cookie by sending an empty value with an expiry in 1970.
        private static bool IsCookieCleared(HttpResponseMessage response)
        {
            var line = GetRefreshSetCookie(response);
            return line is not null
                && line.StartsWith(CookieName + "=;", StringComparison.Ordinal)
                && line.Contains("1970", StringComparison.Ordinal);
        }

        public void Dispose()
        {
            container.Dispose();
        }
    }
}