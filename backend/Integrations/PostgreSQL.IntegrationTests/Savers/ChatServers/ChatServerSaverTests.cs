using Autofac;
using Dapper;
using FluentAssertions;
using HootOut.ChatServers.Entities;
using HootOut.CommonDomain.DefaultValues;
using HootOut.CommonDomain.Persistence;
using HootOut.CommonIntegrationTests.PostgreSQL;
using HootOut.CommonIntegrationTests.S3;
using HootOut.Contracts.Common.Saver;
using HootOut.Infraestructure.DI;
using HootOut.Users.Entities;
using Npgsql;

namespace HootOut.PostgreSQL.IntegrationTests.Savers.ChatServers
{
    [Collection("IntegrationTests")]
    public class ChatServerSaverTests : IClassFixture<PostgresTestContainer>, IClassFixture<TestS3Container>, IAsyncLifetime
    {
        private readonly IContainer container;

        private readonly ClearAllTables clearTables;

        private readonly ISaver<ChatServer> saver;
        private readonly ISaver<UserInfo> userSaver;

        private readonly IPersistenceProvider provider;

        public ChatServerSaverTests(PostgresTestContainer postgresContainer)
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

            saver = container.Resolve<ISaver<ChatServer>>();
            userSaver = container.Resolve<ISaver<UserInfo>>();
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
        public async Task SaveAsync_ValidServer_PersistsAllColumns()
        {
            var ct = TestContext.Current.CancellationToken;
            var user = await CreateUserAsync(ct);
            var server = BuildServer("server", user.Id);

            await saver.SaveAsync(server, ct);

            var row = await GetRowAsync(server.Id, ct);
            row.Should().NotBeNull();
            row.Id.Should().Be(server.Id);
            row.Name.Should().Be(server.Name);
            row.Description.Should().Be(server.Description);
            row.OwnerId.Should().Be(server.OwnerId);
            row.Deleted.Should().BeFalse();
            // timestamptz stores microseconds while DateTime has 100 ns ticks
            row.CreatedAt.Should().BeCloseTo(server.CreatedAt, TimeSpan.FromMilliseconds(1));
            row.ModifiedAt.Should().BeCloseTo(server.ModifiedAt, TimeSpan.FromMilliseconds(1));
        }

        [Fact]
        public async Task SaveAsync_EmptyDescription_PersistsEmptyString()
        {
            var ct = TestContext.Current.CancellationToken;
            var user = await CreateUserAsync(ct);
            var server = BuildServer("server", user.Id);
            server.Description = string.Empty;

            await saver.SaveAsync(server, ct);

            var row = await GetRowAsync(server.Id, ct);
            row!.Description.Should().BeEmpty();
        }

        [Fact]
        public async Task SaveAsync_DuplicatedName_PersistsBoth()
        {
            var ct = TestContext.Current.CancellationToken;

            var user = await CreateUserAsync(ct);
            await saver.SaveAsync(BuildServer("server", user.Id), ct);
            await saver.SaveAsync(BuildServer("server", user.Id), ct);

            (await CountRowsAsync(ct)).Should().Be(2);
        }

        [Fact]
        public async Task SaveAsync_DuplicateId_ThrowsWrappedUniqueViolation()
        {
            var ct = TestContext.Current.CancellationToken;
            var user = await CreateUserAsync(ct);
            var server = BuildServer("server", user.Id);
            await saver.SaveAsync(server, ct);

            var duplicate = BuildServer("duplicate", user.Id);
            duplicate.Id = server.Id;

            var act = () => saver.SaveAsync(duplicate, ct);

            var exception = await act.Should().ThrowExactlyAsync<Exception>()
                .WithMessage($"*{server.Id}*");
            exception.WithInnerException<PostgresException>()
                .Which.SqlState.Should().Be(PostgresErrorCodes.UniqueViolation);

            (await GetRowAsync(server.Id, ct))!.Name.Should().Be("server");
            (await CountRowsAsync(ct)).Should().Be(1);
        }

        [Fact]
        public async Task SaveAsync_CancelledToken_ThrowsOperationCanceledAndDoesNotPersist()
        {
            var ct = TestContext.Current.CancellationToken;
            var user = await CreateUserAsync(ct);
            var server = BuildServer("server", user.Id);
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            var act = () => saver.SaveAsync(server, cts.Token);

            // The token is already cancelled when opening the connection, which happens outside the try/catch
            await act.Should().ThrowAsync<OperationCanceledException>();
            (await CountRowsAsync(TestContext.Current.CancellationToken)).Should().Be(0);
        }
        private async Task<UserInfo> CreateUserAsync(CancellationToken ct)
        {
            var user = Persistable.CreateNew<UserInfo>();
            user.Username = $"User-{user.Id}";
            user.Email = $"{user.Id}@hootout.com";
            user.Password = "12345";
            await userSaver.SaveAsync(user, ct);
            return user;
        }

        private static ChatServer BuildServer(string name, Guid userId)
        {
            var server = Persistable.CreateNew<ChatServer>();
            server.Name = name;
            server.Description = $"Description of {name}";
            server.OwnerId = userId;
            return server;
        }

        private async Task<ChatServerRow?> GetRowAsync(Guid id, CancellationToken ct)
        {
            await using var conn = await provider.OpenConnectionAsync(ct);
            return await conn.QuerySingleOrDefaultAsync<ChatServerRow>(new CommandDefinition(
                """
                    SELECT "Id", "Name", "Description", "OwnerId", "Deleted", "CreatedAt", "ModifiedAt"
                    FROM "HootOut"."ChatServer"
                    WHERE "Id" = @Id
                """,
                new { Id = id },
                cancellationToken: ct));
        }

        private async Task<long> CountRowsAsync(CancellationToken ct)
        {
            await using var conn = await provider.OpenConnectionAsync(ct);
            return await conn.ExecuteScalarAsync<long>(new CommandDefinition(
                """SELECT COUNT(*) FROM "HootOut"."ChatServer" """,
                cancellationToken: ct));
        }

        /// <summary>
        /// Raw table row, including the Deleted column.
        /// </summary>
        private sealed class ChatServerRow
        {
            public Guid Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string? Description { get; set; }
            public Guid OwnerId { get; set; }
            public bool Deleted { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTime ModifiedAt { get; set; }
        }
    }
}
