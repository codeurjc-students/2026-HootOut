using Autofac;
using FluentAssertions;
using HootOut.CommonDomain.DefaultValues;
using HootOut.CommonDomain.Persistence;
using HootOut.CommonIntegrationTests.PostgreSQL;
using HootOut.Contracts.Users.Dtos;
using HootOut.Contracts.Users.Dtos.Request;
using HootOut.Contracts.Users.Services;
using HootOut.Infraestructure.DI;

namespace HootOut.Users.IntegrationTests.Services
{
    [Collection("IntegrationTests")]
    public class UserServiceTests : IClassFixture<PostgresTestContainer>, IAsyncLifetime
    {
        private IContainer container;

        private ClearAllTables clearTables;

        private IUserService userService;

        public UserServiceTests(PostgresTestContainer postgressContainer)
        {
            var builder = new ContainerBuilder();
            new RegistrationManager().RegisterAllAssemblies(builder);
            builder.RegisterInstance(postgressContainer.postgreSQLProvider!).As<IPersistenceProvider>().SingleInstance();
            container = builder.Build();

            var testPersistenceProvider = container.Resolve<IPersistenceProvider>();
            Assert.Same(postgressContainer.postgreSQLProvider, testPersistenceProvider);

            clearTables = container.Resolve<ClearAllTables>();
            var defaultValues = container.Resolve<IEnumerable<IDefaultValues>>().OrderBy(x => x.Priority); ;

            foreach (var value in defaultValues)
            {
                value.InitAsync().Wait();
            }

            userService = container.Resolve<IUserService>();
        }

        public async ValueTask InitializeAsync()
        {

            await clearTables.ClearTablesAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await clearTables.ClearTablesAsync();
        }

        [Theory]
        [InlineData("email@test.com", "username1", "password12345")]
        public async Task GetUserDtoById_One(string username, string email, string password)
        {
            var ct = TestContext.Current.CancellationToken;
            var dto = await userService.CreateUserAsync(new CreateUserRequest
            {
                Username = username,
                Email = email,
                Password = password
            }, ct);

            UserDto? user = await userService.GetUserDtoByIdAsync(dto.Id, ct);
            user.Should().NotBeNull();

            user.Id.Should().NotBeEmpty();
            user.Id.Should().Be(dto.Id);
            user.Username.Should().Be(username);
            user.Email.Should().Be(email);
        }

        [Fact]
        public async Task GetUserDtos_Many()
        {
            var ct = TestContext.Current.CancellationToken;
            await userService.CreateUserAsync(new CreateUserRequest { Email = "1", Password = "1", Username = "1" }, ct);
            var dto = await userService.CreateUserAsync(new CreateUserRequest { Email = "2", Password = "2", Username = "2" }, ct);
            await userService.CreateUserAsync(new CreateUserRequest { Email = "3", Password = "3", Username = "3" }, ct);

            UserDto? user = await userService.GetUserDtoByIdAsync(dto.Id, ct);
            user.Should().NotBeNull();
            user.Id.Should().Be(dto.Id);
        }
    }
}
