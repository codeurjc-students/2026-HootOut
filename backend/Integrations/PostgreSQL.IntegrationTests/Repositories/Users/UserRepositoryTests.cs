using Autofac;
using FluentAssertions;
using HootOut.CommonDomain.DefaultValues;
using HootOut.CommonDomain.Persistence;
using HootOut.CommonIntegrationTests.PostgreSQL;
using HootOut.CommonIntegrationTests.S3;
using HootOut.Contracts.Common.Saver;
using HootOut.Infraestructure.DI;
using HootOut.Users.Entities;
using HootOut.Users.Repositories;

namespace HootOut.PostgreSQL.IntegrationTests.Repositories.Users
{
    [Collection("IntegrationTests")]
    public class UserRepositoryTests : IClassFixture<PostgresTestContainer>, IClassFixture<TestS3Container>, IAsyncLifetime
    {
        private readonly IContainer container;

        private readonly ClearAllTables clearTables;

        private readonly IUserRepository repository;

        private readonly ISaver<UserInfo> saver;

        public UserRepositoryTests(PostgresTestContainer postgresContainer)
        {
            var builder = new ContainerBuilder();
            new RegistrationManager().RegisterAllAssemblies(builder);
            builder.RegisterInstance(postgresContainer.postgreSQLProvider!).As<IPersistenceProvider>().SingleInstance();
            container = builder.Build();

            var testPersistenceProvider = container.Resolve<IPersistenceProvider>();
            Assert.Same(postgresContainer.postgreSQLProvider, testPersistenceProvider);

            clearTables = container.Resolve<ClearAllTables>();
            var defaultValues = container.Resolve<IEnumerable<IDefaultValues>>().OrderBy(x => x.Priority);

            foreach (var value in defaultValues)
            {
                value.InitAsync().Wait();
            }

            repository = container.Resolve<IUserRepository>();
            saver = container.Resolve<ISaver<UserInfo>>();
        }

        public async ValueTask InitializeAsync()
        {
            await clearTables.ClearTablesAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await clearTables.ClearTablesAsync();
            GC.SuppressFinalize(this);
        }

        #region GetAllUsersAsync

        [Fact]
        public async Task GetAllUsers_EmptyTable_ReturnsEmpty()
        {
            var ct = TestContext.Current.CancellationToken;

            var result = await repository.GetAllUsersAsync(ct);

            result.Should().BeEmpty();
        }

        [Fact]
        public async Task GetAllUsers_Many_ReturnsAll()
        {
            var ct = TestContext.Current.CancellationToken;
            var first = await CreateUserAsync("user1", "user1@test.com", ct);
            var second = await CreateUserAsync("user2", "user2@test.com", ct);
            var third = await CreateUserAsync("user3", "user3@test.com", ct);

            var result = (await repository.GetAllUsersAsync(ct)).ToList();

            result.Should().HaveCount(3);
            result.Select(u => u.Id).Should().BeEquivalentTo([first.Id, second.Id, third.Id]);
        }

        #endregion

        #region GetUserByIdAsync

        [Fact]
        public async Task GetUserById_Existing_ReturnsMappedUser()
        {
            var ct = TestContext.Current.CancellationToken;
            var expected = await CreateUserAsync("username1", "user1@test.com", ct);
            await CreateUserAsync("username2", "user2@test.com", ct);

            var result = await repository.GetUserByIdAsync(expected.Id, ct);

            result.Should().NotBeNull();
            AssertMapped(result, expected);
        }

        [Fact]
        public async Task GetUserById_NotExisting_ReturnsNull()
        {
            var ct = TestContext.Current.CancellationToken;
            await CreateUserAsync("username1", "user1@test.com", ct);

            var result = await repository.GetUserByIdAsync(Guid.NewGuid(), ct);

            result.Should().BeNull();
        }

        #endregion

        #region GetUserByEmailAsync

        [Theory]
        [InlineData("user1@test.com")]
        [InlineData("USER1@TEST.COM")]
        [InlineData("User1@Test.Com")]
        public async Task GetUserByEmail_Existing_IsCaseInsensitive(string searchedEmail)
        {
            var ct = TestContext.Current.CancellationToken;
            var expected = await CreateUserAsync("username1", "user1@test.com", ct);
            await CreateUserAsync("username2", "user2@test.com", ct);

            var result = await repository.GetUserByEmailAsync(searchedEmail, ct);

            result.Should().NotBeNull();
            AssertMapped(result, expected);
        }

        [Fact]
        public async Task GetUserByEmail_NotExisting_ReturnsNull()
        {
            var ct = TestContext.Current.CancellationToken;
            await CreateUserAsync("username1", "user1@test.com", ct);

            var result = await repository.GetUserByEmailAsync("missing@test.com", ct);

            result.Should().BeNull();
        }

        [Fact]
        public async Task GetUserByEmail_PartialMatch_ReturnsNull()
        {
            var ct = TestContext.Current.CancellationToken;
            await CreateUserAsync("username1", "user1@test.com", ct);

            var result = await repository.GetUserByEmailAsync("user1@test", ct);

            result.Should().BeNull();
        }

        #endregion

        #region GetUserByUsernameAsync

        [Theory]
        [InlineData("username1")]
        [InlineData("USERNAME1")]
        [InlineData("UserName1")]
        public async Task GetUserByUsername_Existing_IsCaseInsensitive(string searchedUsername)
        {
            var ct = TestContext.Current.CancellationToken;
            var expected = await CreateUserAsync("username1", "user1@test.com", ct);
            await CreateUserAsync("username2", "user2@test.com", ct);

            var result = await repository.GetUserByUsernameAsync(searchedUsername, ct);

            result.Should().NotBeNull();
            AssertMapped(result, expected);
        }

        [Fact]
        public async Task GetUserByUsername_NotExisting_ReturnsNull()
        {
            var ct = TestContext.Current.CancellationToken;
            await CreateUserAsync("username1", "user1@test.com", ct);

            var result = await repository.GetUserByUsernameAsync("missing", ct);

            result.Should().BeNull();
        }

        [Fact]
        public async Task GetUserByUsername_PartialMatch_ReturnsNull()
        {
            var ct = TestContext.Current.CancellationToken;
            await CreateUserAsync("username1", "user1@test.com", ct);

            var result = await repository.GetUserByUsernameAsync("username", ct);

            result.Should().BeNull();
        }

        #endregion

        private async Task<UserInfo> CreateUserAsync(string username, string email, CancellationToken ct)
        {
            var user = Persistable.CreateNew<UserInfo>();
            user.Username = username;
            user.Email = email;
            user.Password = $"hash-{username}";
            await saver.SaveAsync(user, ct);
            return user;
        }

        private static void AssertMapped(UserInfo actual, UserInfo expected)
        {
            actual.Id.Should().Be(expected.Id);
            actual.Username.Should().Be(expected.Username);
            actual.Email.Should().Be(expected.Email);
            // Password is mapped from the PasswordHash column
            actual.Password.Should().Be(expected.Password);
            // timestamptz stores microseconds while DateTime has 100 ns ticks
            actual.CreatedAt.Should().BeCloseTo(expected.CreatedAt, TimeSpan.FromMilliseconds(1));
            // ModifiedAt is not part of the selected columns, so it is not asserted
        }
    }
}
