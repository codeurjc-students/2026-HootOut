using Autofac;
using FluentAssertions;
using HootOut.CommonDomain.DefaultValues;
using HootOut.CommonDomain.Persistence;
using HootOut.CommonIntegrationTests.PostgreSQL;
using HootOut.Contracts.Authentication.Responses;
using HootOut.Contracts.Users.Dtos;
using HootOut.HootOutAPI.IntegrationTests.Common;
using System.Net;
using System.Net.Http.Json;

namespace HootOut.HootOutAPI.IntegrationTests.Controllers
{
    [Collection("IntegrationTests")]
    public class UsersControllerTests : TestControllerBase, IClassFixture<APIFixture>, IDisposable, IAsyncLifetime
    {
        private readonly ILifetimeScope container;
        private ClearAllTables clearTables;

        public UsersControllerTests(APIFixture apiFixture)
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

        [Fact]
        public async Task GetUsersAll_ReturnsUnAuthorized_Empty()
        {
            var ct = TestContext.Current.CancellationToken;
            var response = await httpClient.GetAsync("/api/v1/users/me", ct);

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task GetUsersUserInfo_ReturnsOk_OneUser()
        {
            var ct = TestContext.Current.CancellationToken;

            var response = await httpClient.GetAsync("/api/v1/users/all", ct);
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

            await RegisterAsync(ct);
            var loginResponse = await LoginAsync(DefaultEmail, DefaultPassword, ct);
            var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>(ct);
            auth.Should().NotBeNull();

            response = await SendAuthorizeHttpRequest(
                HttpMethod.Get,
                $"/api/v1/users/me",
                auth?.AccessToken!,
                CancellationToken.None
            );

            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var userDto = await response.Content.ReadFromJsonAsync<UserDto>(ct);
            userDto.Should().NotBeNull();
            userDto.Id.Should().Be(GetUserIdFromAccessToken(auth?.AccessToken));
            userDto.Email.Should().Be(DefaultEmail);
            userDto.Username.Should().Be(DefaultUsername);
        }

        [Fact]
        public async Task GetUserInfo_ReturnsOk_UserFromAuthHeaders()
        {
            var ct = TestContext.Current.CancellationToken;

            await RegisterAsync(ct);
            var loginResponse = await LoginAsync(DefaultEmail, DefaultPassword, ct);
            var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>(ct);
            auth.Should().NotBeNull();

            await RegisterAsync(ct, "DifferentUser", "Different Email", "DifferentPassword");

            var response = await SendAuthorizeHttpRequest(
                HttpMethod.Get,
                $"/api/v1/users/me",
                auth?.AccessToken!,
                ct
            );

            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var userDto = await response.Content.ReadFromJsonAsync<UserDto>(ct);
            userDto.Should().NotBeNull();
            userDto.Id.Should().Be(GetUserIdFromAccessToken(auth?.AccessToken));
            userDto.Email.Should().Be(DefaultEmail);
            userDto.Username.Should().Be(DefaultUsername);
        }

        [Fact]
        public async Task GetUserInfo_ReturnsUnAuthorized_randomToken()
        {
            var ct = TestContext.Current.CancellationToken;
            await RegisterAsync(ct);

            var response = await SendAuthorizeHttpRequest(
                HttpMethod.Get,
                $"/api/v1/users/me",
                "12345",
                ct
            );

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        public void Dispose()
        {
            container.Dispose();
        }
    }
}