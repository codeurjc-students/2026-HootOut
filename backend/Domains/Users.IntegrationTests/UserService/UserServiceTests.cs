using Autofac;
using FluentAssertions;
using HootOut.CommonDomain.DefaultValues;
using HootOut.CommonDomain.Persistence;
using HootOut.CommonIntegrationTests.PostgreSQL;
using HootOut.Contracts.Users.Dtos;
using HootOut.Contracts.Users.Dtos.Request;
using HootOut.Contracts.Users.Services;
using HootOut.Infraestructure.DI;

namespace HootOut.Users.IntegrationTests.UserService
{

    public class UserServiceTests : IClassFixture<PostgresTestContainer>, IAsyncLifetime
    {
        private IContainer container;

        private ClearAllTables clearTables;

        private IUserService userService;

        public UserServiceTests(PostgresTestContainer postgressContainer)
        {
            var builder = new ContainerBuilder();
            new RegistrationManager().RegisterAllAssemblies(builder);
            builder.RegisterInstance(postgressContainer.postgreSQLProvider).As<IPersistenceProvider>().SingleInstance();
            container = builder.Build();

            var testPersistenceProvider = container.Resolve<IPersistenceProvider>();
            Assert.Same(postgressContainer.postgreSQLProvider, testPersistenceProvider);

            clearTables = container.Resolve<ClearAllTables>();
            var defaultValues = container.Resolve<IEnumerable<IDefaultValues>>().OrderBy(x => x.Priority); ;

            foreach (var value in defaultValues)
            {
                value.Init();
            }

            userService = container.Resolve<IUserService>();
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
        public async Task GetUserDtos_Emtpy()
        {
            userService.GetUserDtos().Should().BeEmpty();
        }

        [Theory]
        [InlineData("email@test.com", "username1", "password12345")]
        public async Task GetUserDtos_One(string username, string email, string password)
        {
            userService.CreateUser(new CreateUserRequest
            {
                Username = username,
                Email = email,
                Password = password
            });

            IEnumerable<UserDto> users = userService.GetUserDtos();
            users.Should().HaveCount(1);

            UserDto user = users.Single();

            user.Uid.Should().NotBeEmpty();
            user.Username.Should().Be(username);
            user.Email.Should().Be(email);
            user.Password.Should().NotBe(password);
        }

        [Fact]
        public async Task GetUserDtos_Many()
        {
            userService.CreateUser(new CreateUserRequest { Email = "1", Password = "1", Username = "1" });
            userService.CreateUser(new CreateUserRequest { Email = "2", Password = "2", Username = "2" });
            userService.CreateUser(new CreateUserRequest { Email = "3", Password = "3", Username = "3" });

            userService.GetUserDtos().Should().HaveCount(3); 
        }
    }
}
