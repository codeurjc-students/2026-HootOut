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
    public class ServerMemberSaverTests : IClassFixture<PostgresTestContainer>, IClassFixture<TestS3Container>, IAsyncLifetime
    {
        private readonly IContainer container;

        private readonly ClearAllTables clearTables;

        private readonly ISaver<ServerMember> saver;

        private readonly ISaver<ChatServer> serverSaver;

        private readonly ISaver<UserInfo> userSaver;

        private readonly IPersistenceProvider provider;

        public ServerMemberSaverTests(PostgresTestContainer postgresContainer)
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

            saver = container.Resolve<ISaver<ServerMember>>();
            serverSaver = container.Resolve<ISaver<ChatServer>>();
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
        public async Task SaveAsync_ValidMemberWithoutInvitation_PersistsAllColumns()
        {
            var ct = TestContext.Current.CancellationToken;
            var user = await CreateUserAsync("user1", ct);
            var server = await CreateServerAsync(ct);
            var member = BuildMember(server.Id, user.Id);

            await saver.SaveAsync(member, ct);

            var row = await GetRowAsync(member.Id, ct);
            row.Should().NotBeNull();
            row.Id.Should().Be(member.Id);
            row.ChatServerId.Should().Be(server.Id);
            row.UserId.Should().Be(user.Id);
            row.InvitationId.Should().BeNull();
            row.Deleted.Should().BeFalse();
            // timestamptz stores microseconds while DateTime has 100 ns ticks
            row.CreatedAt.Should().BeCloseTo(member.CreatedAt, TimeSpan.FromMilliseconds(1));
            row.ModifiedAt.Should().BeCloseTo(member.ModifiedAt, TimeSpan.FromMilliseconds(1));
        }

        [Fact]
        public async Task SaveAsync_SameUserInSeveralServers_PersistsAll()
        {
            var ct = TestContext.Current.CancellationToken;
            var user = await CreateUserAsync("user1", ct);
            var server = await CreateServerAsync(ct);
            var otherServer = await CreateServerAsync(ct);

            await saver.SaveAsync(BuildMember(server.Id, user.Id), ct);
            await saver.SaveAsync(BuildMember(otherServer.Id, user.Id), ct);

            (await CountRowsAsync(ct)).Should().Be(2);
        }

        [Fact]
        public async Task SaveAsync_SameUserTwiceInSameServer_ThrowsWrappedUniqueViolation()
        {
            var ct = TestContext.Current.CancellationToken;
            var user = await CreateUserAsync("user1", ct);
            var server = await CreateServerAsync(ct);
            await saver.SaveAsync(BuildMember(server.Id, user.Id), ct);

            var duplicate = BuildMember(server.Id, user.Id);

            var act = () => saver.SaveAsync(duplicate, ct);

            var exception = await act.Should().ThrowExactlyAsync<Exception>()
                .WithMessage($"*{duplicate.Id}*");
            exception.WithInnerException<PostgresException>()
                .Which.ConstraintName.Should().Be("ux_server_member_active");
            (await CountRowsAsync(ct)).Should().Be(1);
        }

        [Fact]
        public async Task SaveAsync_UserRejoinsAfterLeaving_PersistsNewMembership()
        {
            var ct = TestContext.Current.CancellationToken;
            var user = await CreateUserAsync("user1", ct);
            var server = await CreateServerAsync(ct);
            var first = BuildMember(server.Id, user.Id);
            await saver.SaveAsync(first, ct);
            await MarkAsDeletedAsync(first.Id, ct);

            var rejoin = BuildMember(server.Id, user.Id);
            await saver.SaveAsync(rejoin, ct);

            // The unique index only applies to active memberships
            (await GetRowAsync(first.Id, ct))!.Deleted.Should().BeTrue();
            (await GetRowAsync(rejoin.Id, ct))!.Deleted.Should().BeFalse();
            (await CountRowsAsync(ct)).Should().Be(2);
        }

        [Fact]
        public async Task SaveAsync_NotExistingServer_ThrowsWrappedForeignKeyViolation()
        {
            var ct = TestContext.Current.CancellationToken;
            var user = await CreateUserAsync("user1", ct);
            var member = BuildMember(Guid.NewGuid(), user.Id);

            var act = () => saver.SaveAsync(member, ct);

            var exception = await act.Should().ThrowExactlyAsync<Exception>();
            exception.WithInnerException<PostgresException>()
                .Which.SqlState.Should().Be(PostgresErrorCodes.ForeignKeyViolation);
            (await CountRowsAsync(ct)).Should().Be(0);
        }

        [Fact]
        public async Task SaveAsync_NotExistingUser_ThrowsWrappedForeignKeyViolation()
        {
            var ct = TestContext.Current.CancellationToken;
            var server = await CreateServerAsync(ct);
            var member = BuildMember(server.Id, Guid.NewGuid());

            var act = () => saver.SaveAsync(member, ct);

            var exception = await act.Should().ThrowExactlyAsync<Exception>();
            exception.WithInnerException<PostgresException>()
                .Which.SqlState.Should().Be(PostgresErrorCodes.ForeignKeyViolation);
            (await CountRowsAsync(ct)).Should().Be(0);
        }

        [Fact]
        public async Task SaveAsync_NotExistingInvitation_ThrowsWrappedForeignKeyViolation()
        {
            var ct = TestContext.Current.CancellationToken;
            var user = await CreateUserAsync("user1", ct);
            var server = await CreateServerAsync(ct);
            var member = BuildMember(server.Id, user.Id);
            member.InvitationId = Guid.NewGuid();

            var act = () => saver.SaveAsync(member, ct);

            var exception = await act.Should().ThrowExactlyAsync<Exception>();
            exception.WithInnerException<PostgresException>()
                .Which.SqlState.Should().Be(PostgresErrorCodes.ForeignKeyViolation);
            (await CountRowsAsync(ct)).Should().Be(0);
        }

        [Fact]
        public async Task SaveAsync_CancelledToken_ThrowsOperationCanceledAndDoesNotPersist()
        {
            var ct = TestContext.Current.CancellationToken;
            var user = await CreateUserAsync("user1", ct);
            var server = await CreateServerAsync(ct);
            var member = BuildMember(server.Id, user.Id);
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            var act = () => saver.SaveAsync(member, cts.Token);

            // The token is already cancelled when opening the connection, which happens outside the try/catch
            await act.Should().ThrowAsync<OperationCanceledException>();
            (await CountRowsAsync(ct)).Should().Be(0);
        }

        private async Task<UserInfo> CreateUserAsync(string username, CancellationToken ct)
        {
            var user = Persistable.CreateNew<UserInfo>();
            user.Username = username;
            user.Email = $"{username}@test.com";
            user.Password = $"hash-{username}";
            await userSaver.SaveAsync(user, ct);
            return user;
        }

        private async Task<ChatServer> CreateServerAsync(CancellationToken ct)
        {
            var server = Persistable.CreateNew<ChatServer>();
            server.Name = $"server-{server.Id}";
            server.Description = "Test server";
            server.OwnerId = Guid.NewGuid();
            await serverSaver.SaveAsync(server, ct);
            return server;
        }

        private static ServerMember BuildMember(Guid serverId, Guid userId)
        {
            var member = Persistable.CreateNew<ServerMember>();
            member.ChatServerId = serverId;
            member.UserId = userId;
            return member;
        }

        private async Task<ServerMemberRow?> GetRowAsync(Guid id, CancellationToken ct)
        {
            await using var conn = await provider.OpenConnectionAsync(ct);
            return await conn.QuerySingleOrDefaultAsync<ServerMemberRow>(new CommandDefinition(
                """
                    SELECT "Id", "ChatServerId", "UserId", "InvitationId", "Deleted", "CreatedAt", "ModifiedAt"
                    FROM "HootOut"."ServerMember"
                    WHERE "Id" = @Id
                """,
                new { Id = id },
                cancellationToken: ct));
        }

        private async Task MarkAsDeletedAsync(Guid id, CancellationToken ct)
        {
            await using var conn = await provider.OpenConnectionAsync(ct);
            await conn.ExecuteAsync(new CommandDefinition(
                """UPDATE "HootOut"."ServerMember" SET "Deleted" = true WHERE "Id" = @Id""",
                new { Id = id },
                cancellationToken: ct));
        }

        private async Task<long> CountRowsAsync(CancellationToken ct)
        {
            await using var conn = await provider.OpenConnectionAsync(ct);
            return await conn.ExecuteScalarAsync<long>(new CommandDefinition(
                """SELECT COUNT(*) FROM "HootOut"."ServerMember" """,
                cancellationToken: ct));
        }

        /// <summary>
        /// Raw table row, including the Deleted column.
        /// </summary>
        private sealed class ServerMemberRow
        {
            public Guid Id { get; set; }
            public Guid ChatServerId { get; set; }
            public Guid UserId { get; set; }
            public Guid? InvitationId { get; set; }
            public bool Deleted { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTime ModifiedAt { get; set; }
        }
    }
}
