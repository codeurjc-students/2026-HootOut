using Autofac;
using HootOut.CommonDomain.DefaultValues;
using HootOut.CommonDomain.Persistence;
using HootOut.CommonIntegrationTests.PostgreSQL;
using HootOut.CommonIntegrationTests.Services;
using HootOut.Contracts.Users.Dtos;
using HootOut.Contracts.Users.Dtos.Request;
using HootOut.Contracts.Users.Search;
using HootOut.Contracts.Users.Services;
using HootOut.Infraestructure.DI;
using HootOut.PostgreSQL.Savers.Users;
using HootOut.PostgreSQL.Searchs.Users;
using HootOut.Users.Entities;

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
            RegistratorManager.RegisterAllAssemblies(builder);
            builder.RegisterInstance(postgressContainer.postgreSQLProvider).As<IPersistenceProvider>().SingleInstance();
            container = builder.Build();

            var testPersistenceProvider = container.Resolve<IPersistenceProvider>();
            Assert.Same(postgressContainer.postgreSQLProvider, testPersistenceProvider);

            clearTables = container.Resolve<ClearAllTables>();
            var defaultValues = container.Resolve<IEnumerable<IDefaultValues>>();

            foreach (var value in defaultValues)
            {
                value.Init();
            }

            userService = container.Resolve<IUserService>();
        }

        public ValueTask InitializeAsync()
        {
            //Clear DB
            if (clearTables != null)
            {
                clearTables.ClearTables();
            }
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            //Clear DB
            if (clearTables != null)
            {
                clearTables.ClearTables();
            }
            return ValueTask.CompletedTask;
        }

        [Fact]
        public async Task GetUserList_Empty()
        {
            Assert.Empty(userService.GetUserDtos());
        }

        [Theory]
        [InlineData("email@test.com", "username1", "password12345")]
        public async Task GetUserList_One(string username, string email, string password)
        {
            userService.CreateUser(new CreateUserRequest
            {
                Username = username,
                Email = email,
                Password = password
            });

            IEnumerable<UserDto> users = userService.GetUserDtos();
            Assert.Single(users);
            UserDto user = users.Single();
            Assert.NotEqual(Guid.Empty, user.Uid);
            Assert.Equal(username, user.Username);
            Assert.Equal(email, user.Email);
            Assert.NotEqual(password, user.Password);
        }

        [Fact]
        public async Task GetUserList_Many()
        {
            userService.CreateUser(new CreateUserRequest { Email = "1", Password = "1", Username = "1" });
            userService.CreateUser(new CreateUserRequest { Email = "2", Password = "2", Username = "2" });
            userService.CreateUser(new CreateUserRequest { Email = "3", Password = "3", Username = "3" });

            Assert.Equal(3, userService.GetUserDtos().Count());
        } 
    }
}
