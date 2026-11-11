using Autofac;
using Dapper;
using FluentAssertions;
using HootOut.CommonDomain.DefaultValues;
using HootOut.CommonDomain.Persistence;
using HootOut.CommonIntegrationTests.PostgreSQL;
using HootOut.CommonIntegrationTests.S3;
using HootOut.Contracts.Common.Saver;
using HootOut.Infraestructure.DI;
using HootOut.Users.Entities;
using Npgsql;

namespace HootOut.PostgreSQL.IntegrationTests.Savers.Users
{
    [Collection("IntegrationTests")]
    public class UserSaverTests : IClassFixture<PostgresTestContainer>, IClassFixture<TestS3Container>, IAsyncLifetime
    {
        private readonly IContainer container;

        private readonly ClearAllTables clearTables;

        private readonly ISaver<UserInfo> saver;

        private readonly IPersistenceProvider provider;

        public UserSaverTests(PostgresTestContainer postgresContainer)
        {
            var builder = new ContainerBuilder();
            new RegistrationManager().RegisterAllAssemblies(builder);
            builder.RegisterInstance(postgresContainer.postgreSQLProvider!).As<IPersistenceProvider>().SingleInstance();
            container = builder.Build();

            provider = container.Resolve<IPersistenceProvider>();
            Assert.Same(postgresContainer.postgreSQLProvider, provider);

            clearTables = container.Resolve<ClearAllTables>();
            var defaultValues = container.Resolve<IEnumerable<IDefaultValues>>().OrderBy(x => x.Priority);

            foreach (var value in defaultValues)
            {
                value.InitAsync().Wait();
            }

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

        [Fact]
        public async Task SaveAsync_ValidUser_PersistsAllColumns()
        {
            var ct = TestContext.Current.CancellationToken;
            var user = BuildUser("username1", "user1@test.com");

            await saver.SaveAsync(user, ct);

            var row = await GetRowAsync(user.Id, ct);
            row.Should().NotBeNull();
            row.Id.Should().Be(user.Id);
            row.Username.Should().Be(user.Username);
            row.Email.Should().Be(user.Email);
            // The saver stores Password as is; hashing is not its responsibility
            row.PasswordHash.Should().Be(user.Password);
            // timestamptz stores microseconds while DateTime has 100 ns ticks
            row.CreatedAt.Should().BeCloseTo(user.CreatedAt, TimeSpan.FromMilliseconds(1));
            row.ModifiedAt.Should().BeCloseTo(user.ModifiedAt, TimeSpan.FromMilliseconds(1));
        }

        [Fact]
        public async Task SaveAsync_Many_PersistsEveryRow()
        {
            var ct = TestContext.Current.CancellationToken;

            await saver.SaveAsync(BuildUser("username1", "user1@test.com"), ct);
            await saver.SaveAsync(BuildUser("username2", "user2@test.com"), ct);
            await saver.SaveAsync(BuildUser("username3", "user3@test.com"), ct);

            (await CountRowsAsync(ct)).Should().Be(3);
        }

        [Fact]
        public async Task SaveAsync_DuplicateId_ThrowsWrappedUniqueViolation()
        {
            var ct = TestContext.Current.CancellationToken;
            var user = BuildUser("username1", "user1@test.com");
            await saver.SaveAsync(user, ct);

            var duplicate = BuildUser("username2", "user2@test.com");
            duplicate.Id = user.Id;

            var act = () => saver.SaveAsync(duplicate, ct);

            var exception = await act.Should().ThrowExactlyAsync<Exception>()
                .WithMessage($"*{user.Id}*");
            exception.WithInnerException<PostgresException>()
                .Which.SqlState.Should().Be(PostgresErrorCodes.UniqueViolation);

            var row = await GetRowAsync(user.Id, ct);
            row!.Username.Should().Be("username1");
            (await CountRowsAsync(ct)).Should().Be(1);
        }

        [Theory]
        [InlineData("username1")]
        [InlineData("USERNAME1")]
        [InlineData("UserName1")]
        public async Task SaveAsync_DuplicateUsernameIgnoringCase_ThrowsWrappedUniqueViolation(string duplicatedUsername)
        {
            var ct = TestContext.Current.CancellationToken;
            await saver.SaveAsync(BuildUser("username1", "user1@test.com"), ct);

            var duplicate = BuildUser(duplicatedUsername, "user2@test.com");

            var act = () => saver.SaveAsync(duplicate, ct);

            var exception = await act.Should().ThrowExactlyAsync<Exception>()
                .WithMessage($"*{duplicate.Id}*");
            exception.WithInnerException<PostgresException>()
                .Which.ConstraintName.Should().Be("ix_users_username_lower");

            (await CountRowsAsync(ct)).Should().Be(1);
        }

        [Theory]
        [InlineData("user1@test.com")]
        [InlineData("USER1@TEST.COM")]
        [InlineData("User1@Test.Com")]
        public async Task SaveAsync_DuplicateEmailIgnoringCase_ThrowsWrappedUniqueViolation(string duplicatedEmail)
        {
            var ct = TestContext.Current.CancellationToken;
            await saver.SaveAsync(BuildUser("username1", "user1@test.com"), ct);

            var duplicate = BuildUser("username2", duplicatedEmail);

            var act = () => saver.SaveAsync(duplicate, ct);

            var exception = await act.Should().ThrowExactlyAsync<Exception>()
                .WithMessage($"*{duplicate.Id}*");
            exception.WithInnerException<PostgresException>()
                .Which.ConstraintName.Should().Be("ix_users_email_lower");

            (await CountRowsAsync(ct)).Should().Be(1);
        }

        [Fact]
        public async Task SaveAsync_CancelledToken_ThrowsOperationCanceledAndDoesNotPersist()
        {
            var user = BuildUser("username1", "user1@test.com");
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            var act = () => saver.SaveAsync(user, cts.Token);

            // The token is already cancelled when opening the connection, which happens outside the try/catch
            await act.Should().ThrowAsync<OperationCanceledException>();
            (await CountRowsAsync(TestContext.Current.CancellationToken)).Should().Be(0);
        }

        private static UserInfo BuildUser(string username, string email)
        {
            var user = Persistable.CreateNew<UserInfo>();
            user.Username = username;
            user.Email = email;
            user.Password = $"hash-{username}";
            return user;
        }

        private async Task<UserInfoRow?> GetRowAsync(Guid id, CancellationToken ct)
        {
            await using var conn = await provider.OpenConnectionAsync(ct);
            return await conn.QuerySingleOrDefaultAsync<UserInfoRow>(new CommandDefinition(
                """
                    SELECT "Id", "Username", "Email", "PasswordHash", "CreatedAt", "ModifiedAt"
                    FROM "HootOut"."UserInfo"
                    WHERE "Id" = @Id
                """,
                new { Id = id },
                cancellationToken: ct));
        }

        private async Task<long> CountRowsAsync(CancellationToken ct)
        {
            await using var conn = await provider.OpenConnectionAsync(ct);
            return await conn.ExecuteScalarAsync<long>(new CommandDefinition(
                """SELECT COUNT(*) FROM "HootOut"."UserInfo" """,
                cancellationToken: ct));
        }

        /// <summary>
        /// Raw table row, using the real column names.
        /// </summary>
        private sealed class UserInfoRow
        {
            public Guid Id { get; set; }
            public string Username { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string PasswordHash { get; set; } = string.Empty;
            public DateTime CreatedAt { get; set; }
            public DateTime ModifiedAt { get; set; }
        }
    }
}
