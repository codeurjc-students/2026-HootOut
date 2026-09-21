using Autofac;
using FluentAssertions;
using HootOut.CommonDomain.DefaultValues;
using HootOut.CommonDomain.Persistence;
using HootOut.CommonIntegrationTests.PostgreSQL;
using HootOut.Contracts.Users.Dtos;
using HootOut.Contracts.Users.Dtos.Request;
using HootOut.Contracts.Users.Services;
using HootOut.HootOutAPI.IntegrationTests.Common;
using System.Net;
using System.Net.Http.Json;

namespace HootOut.HootOutAPI.IntegrationTests.Controllers
{
    public class UserControllerTests : IClassFixture<APIFixture>, IDisposable, IAsyncLifetime
    {
        private readonly ILifetimeScope container;
        private readonly HttpClient httpClient;
        private ClearAllTables clearTables;

        public UserControllerTests(APIFixture apiFixture)
        {
            httpClient = apiFixture.CreateClient();
            container = apiFixture.AutofacRoot.BeginLifetimeScope();

            var testPersistenceProvider = container.Resolve<IPersistenceProvider>();
            Assert.Same(apiFixture.postgreSQLProvider, testPersistenceProvider);

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
        public async Task GetUsersAll_ReturnsOk_Empty()
        {
            var response = await httpClient.GetAsync("/api/v1/user/all", CancellationToken.None);

            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var messages = await response.Content.ReadFromJsonAsync<IEnumerable<UserDto>>(CancellationToken.None);
            messages.Should().NotBeNull();
            messages.Should().BeEmpty();
        }

        [Fact]
        public async Task GetUsersAll_ReturnsOk_OneUser()
        {
            var userService = container.Resolve<IUserService>();

            userService.CreateUser(new CreateUserRequest { Username = "Test1", Email = "email@email.com", Password = "test1" });

            var response = await httpClient.GetAsync("/api/v1/user/all", CancellationToken.None);

            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var users = await response.Content.ReadFromJsonAsync<IEnumerable<UserDto>>(CancellationToken.None);
            users.Should().NotBeNull();
            users.Should().HaveCount(1);

            UserDto user = users.First();
            user.Username.Should().Be("Test1");
        }

        [Fact]
        public async Task GetUsersAll_ReturnsOk_MultipleUsers()
        {
            var userService = container.Resolve<IUserService>();

            userService.CreateUser(new CreateUserRequest { Username = "1", Email = "1", Password = "1" });
            userService.CreateUser(new CreateUserRequest { Username = "2", Email = "2", Password = "2" });
            userService.CreateUser(new CreateUserRequest { Username = "3", Email = "3", Password = "3" });

            var response = await httpClient.GetAsync("/api/v1/user/all", CancellationToken.None);

            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var users = await response.Content.ReadFromJsonAsync<IEnumerable<UserDto>>(CancellationToken.None);
            users.Should().NotBeNull();
            users.Should().HaveCount(3);
        }

        public void Dispose()
        {
            container.Dispose();
        }
    }
}