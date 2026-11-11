using Autofac;
using Dapper;
using FluentAssertions;
using HootOut.ChatServers.Entities;
using HootOut.CommonDomain.DefaultValues;
using HootOut.CommonDomain.Persistence;
using HootOut.CommonIntegrationTests.PostgreSQL;
using HootOut.Contracts.ChatServers.Enums;
using HootOut.Contracts.Common.Saver;
using HootOut.Infraestructure.DI;
using HootOut.Users.Entities;
using Npgsql;

namespace HootOut.PostgreSQL.IntegrationTests.Savers.ChatServers
{
    [Collection("IntegrationTests")]
    public class ServerInvitationSaverTests : IClassFixture<PostgresTestContainer>, IAsyncLifetime
    {
        private readonly IContainer container;

        private readonly ClearAllTables clearTables;

        private readonly ISaver<ServerInvitation> saver;

        private readonly ISaver<ChatServer> serverSaver;

        private readonly ISaver<UserInfo> userSaver;

        private readonly IPersistenceProvider provider;

        public ServerInvitationSaverTests(PostgresTestContainer postgresContainer)
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

            saver = container.Resolve<ISaver<ServerInvitation>>();
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

        #region Insert

        [Theory]
        [InlineData(ServerInvitationStatus.Pending)]
        [InlineData(ServerInvitationStatus.Accepted)]
        [InlineData(ServerInvitationStatus.Rejected)]
        [InlineData(ServerInvitationStatus.Revoked)]
        public async Task SaveAsync_NewInvitation_PersistsAllColumns(ServerInvitationStatus status)
        {
            var ct = TestContext.Current.CancellationToken;
            var (server, inviter, user) = await CreateScenarioAsync(ct);
            var invitation = BuildInvitation(server.Id, inviter.Id, user.Id, status);

            await saver.SaveAsync(invitation, ct);

            var row = await GetRowAsync(invitation.Id, ct);
            row.Should().NotBeNull();
            row.Id.Should().Be(invitation.Id);
            row.ChatServerId.Should().Be(server.Id);
            row.InvitedBy.Should().Be(inviter.Id);
            row.UserId.Should().Be(user.Id);
            row.Status.Should().Be((short)status);
            // timestamptz stores microseconds while DateTime has 100 ns ticks
            row.CreatedAt.Should().BeCloseTo(invitation.CreatedAt, TimeSpan.FromMilliseconds(1));
            row.ModifiedAt.Should().BeCloseTo(invitation.ModifiedAt, TimeSpan.FromMilliseconds(1));
        }

        [Fact]
        public async Task SaveAsync_SecondPendingInvitationForSameUserAndServer_ThrowsWrappedUniqueViolation()
        {
            var ct = TestContext.Current.CancellationToken;
            var (server, inviter, user) = await CreateScenarioAsync(ct);
            await saver.SaveAsync(BuildInvitation(server.Id, inviter.Id, user.Id, ServerInvitationStatus.Pending), ct);

            var duplicate = BuildInvitation(server.Id, inviter.Id, user.Id, ServerInvitationStatus.Pending);

            var act = () => saver.SaveAsync(duplicate, ct);

            var exception = await act.Should().ThrowExactlyAsync<Exception>()
                .WithMessage($"*{duplicate.Id}*");
            exception.WithInnerException<PostgresException>()
                .Which.ConstraintName.Should().Be("ux_server_invitation_pending");
            (await CountRowsAsync(ct)).Should().Be(1);
        }

        [Theory]
        [InlineData(ServerInvitationStatus.Accepted)]
        [InlineData(ServerInvitationStatus.Rejected)]
        [InlineData(ServerInvitationStatus.Revoked)]
        public async Task SaveAsync_NewPendingAfterPreviousWasClosed_Persists(ServerInvitationStatus previousStatus)
        {
            var ct = TestContext.Current.CancellationToken;
            var (server, inviter, user) = await CreateScenarioAsync(ct);
            await saver.SaveAsync(BuildInvitation(server.Id, inviter.Id, user.Id, previousStatus), ct);

            await saver.SaveAsync(BuildInvitation(server.Id, inviter.Id, user.Id, ServerInvitationStatus.Pending), ct);

            (await CountRowsAsync(ct)).Should().Be(2);
        }

        [Fact]
        public async Task SaveAsync_PendingInvitationsForSameUserInDifferentServers_PersistsAll()
        {
            var ct = TestContext.Current.CancellationToken;
            var (server, inviter, user) = await CreateScenarioAsync(ct);
            var otherServer = await CreateServerAsync("other", inviter.Id, ct);

            await saver.SaveAsync(BuildInvitation(server.Id, inviter.Id, user.Id, ServerInvitationStatus.Pending), ct);
            await saver.SaveAsync(BuildInvitation(otherServer.Id, inviter.Id, user.Id, ServerInvitationStatus.Pending), ct);

            (await CountRowsAsync(ct)).Should().Be(2);
        }

        [Fact]
        public async Task SaveAsync_InvalidStatus_ThrowsWrappedCheckViolation()
        {
            var ct = TestContext.Current.CancellationToken;
            var (server, inviter, user) = await CreateScenarioAsync(ct);
            var invitation = BuildInvitation(server.Id, inviter.Id, user.Id, (ServerInvitationStatus)5);

            var act = () => saver.SaveAsync(invitation, ct);

            var exception = await act.Should().ThrowExactlyAsync<Exception>();
            exception.WithInnerException<PostgresException>()
                .Which.SqlState.Should().Be(PostgresErrorCodes.CheckViolation);
            (await CountRowsAsync(ct)).Should().Be(0);
        }

        [Theory]
        [InlineData("server")]
        [InlineData("invitedBy")]
        [InlineData("user")]
        public async Task SaveAsync_NotExistingReference_ThrowsWrappedForeignKeyViolation(string missingReference)
        {
            var ct = TestContext.Current.CancellationToken;
            var (server, inviter, user) = await CreateScenarioAsync(ct);
            var invitation = BuildInvitation(
                missingReference == "server" ? Guid.NewGuid() : server.Id,
                missingReference == "invitedBy" ? Guid.NewGuid() : inviter.Id,
                missingReference == "user" ? Guid.NewGuid() : user.Id,
                ServerInvitationStatus.Pending);

            var act = () => saver.SaveAsync(invitation, ct);

            var exception = await act.Should().ThrowExactlyAsync<Exception>();
            exception.WithInnerException<PostgresException>()
                .Which.SqlState.Should().Be(PostgresErrorCodes.ForeignKeyViolation);
            (await CountRowsAsync(ct)).Should().Be(0);
        }

        #endregion

        #region Update

        [Theory]
        [InlineData(ServerInvitationStatus.Accepted)]
        [InlineData(ServerInvitationStatus.Rejected)]
        [InlineData(ServerInvitationStatus.Revoked)]
        public async Task SaveAsync_ExistingInvitation_UpdatesStatusAndModifiedAtOnly(ServerInvitationStatus newStatus)
        {
            var ct = TestContext.Current.CancellationToken;
            var (server, inviter, user) = await CreateScenarioAsync(ct);
            var otherServer = await CreateServerAsync("other", inviter.Id, ct);
            var otherUser = await CreateUserAsync("other", ct);
            var original = BuildInvitation(server.Id, inviter.Id, user.Id, ServerInvitationStatus.Pending);
            await saver.SaveAsync(original, ct);

            var updated = new ServerInvitation
            {
                Id = original.Id,
                ChatServerId = otherServer.Id,
                InvitedBy = otherUser.Id,
                UserId = otherUser.Id,
                Status = newStatus,
                CreatedAt = original.CreatedAt.AddDays(-10),
                ModifiedAt = original.ModifiedAt.AddDays(1)
            };

            await saver.SaveAsync(updated, ct);

            var row = await GetRowAsync(original.Id, ct);
            row.Should().NotBeNull();
            row.Status.Should().Be((short)newStatus);
            row.ModifiedAt.Should().BeCloseTo(updated.ModifiedAt, TimeSpan.FromMilliseconds(1));
            row.ChatServerId.Should().Be(server.Id);
            row.InvitedBy.Should().Be(inviter.Id);
            row.UserId.Should().Be(user.Id);
            row.CreatedAt.Should().BeCloseTo(original.CreatedAt, TimeSpan.FromMilliseconds(1));
            (await CountRowsAsync(ct)).Should().Be(1);
        }

        [Fact]
        public async Task SaveAsync_ReopeningWhenAnotherPendingExists_ThrowsWrappedUniqueViolation()
        {
            var ct = TestContext.Current.CancellationToken;
            var (server, inviter, user) = await CreateScenarioAsync(ct);
            var rejected = BuildInvitation(server.Id, inviter.Id, user.Id, ServerInvitationStatus.Rejected);
            await saver.SaveAsync(rejected, ct);
            await saver.SaveAsync(BuildInvitation(server.Id, inviter.Id, user.Id, ServerInvitationStatus.Pending), ct);

            rejected.Status = ServerInvitationStatus.Pending;
            var act = () => saver.SaveAsync(rejected, ct);

            var exception = await act.Should().ThrowExactlyAsync<Exception>();
            exception.WithInnerException<PostgresException>()
                .Which.ConstraintName.Should().Be("ux_server_invitation_pending");
            (await GetRowAsync(rejected.Id, ct))!.Status.Should().Be((short)ServerInvitationStatus.Rejected);
        }

        #endregion

        [Fact]
        public async Task SaveAsync_CancelledToken_ThrowsOperationCanceledAndDoesNotPersist()
        {
            var ct = TestContext.Current.CancellationToken;
            var (server, inviter, user) = await CreateScenarioAsync(ct);
            var invitation = BuildInvitation(server.Id, inviter.Id, user.Id, ServerInvitationStatus.Pending);
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            var act = () => saver.SaveAsync(invitation, cts.Token);

            // The token is already cancelled when opening the connection, which happens outside the try/catch
            await act.Should().ThrowAsync<OperationCanceledException>();
            (await CountRowsAsync(ct)).Should().Be(0);
        }

        private async Task<(ChatServer Server, UserInfo Inviter, UserInfo User)> CreateScenarioAsync(CancellationToken ct)
        {
            var inviter = await CreateUserAsync("inviter", ct);
            var user = await CreateUserAsync("user", ct);
            var server = await CreateServerAsync("server", inviter.Id, ct);
            return (server, inviter, user);
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

        private async Task<ChatServer> CreateServerAsync(string name, Guid ownerId, CancellationToken ct)
        {
            var server = Persistable.CreateNew<ChatServer>();
            server.Name = name;
            server.Description = $"Description of {name}";
            server.OwnerId = ownerId;
            await serverSaver.SaveAsync(server, ct);
            return server;
        }

        private static ServerInvitation BuildInvitation(Guid serverId, Guid invitedBy, Guid userId, ServerInvitationStatus status)
        {
            var invitation = Persistable.CreateNew<ServerInvitation>();
            invitation.ChatServerId = serverId;
            invitation.InvitedBy = invitedBy;
            invitation.UserId = userId;
            invitation.Status = status;
            return invitation;
        }

        private async Task<ServerInvitationRow?> GetRowAsync(Guid id, CancellationToken ct)
        {
            await using var conn = await provider.OpenConnectionAsync(ct);
            return await conn.QuerySingleOrDefaultAsync<ServerInvitationRow>(new CommandDefinition(
                """
                    SELECT "Id", "ChatServerId", "InvitedBy", "UserId", "Status", "CreatedAt", "ModifiedAt"
                    FROM "HootOut"."ServerInvitation"
                    WHERE "Id" = @Id
                """,
                new { Id = id },
                cancellationToken: ct));
        }

        private async Task<long> CountRowsAsync(CancellationToken ct)
        {
            await using var conn = await provider.OpenConnectionAsync(ct);
            return await conn.ExecuteScalarAsync<long>(new CommandDefinition(
                """SELECT COUNT(*) FROM "HootOut"."ServerInvitation" """,
                cancellationToken: ct));
        }

        /// <summary>
        /// Raw table row; Status is read as the stored smallint value.
        /// </summary>
        private sealed class ServerInvitationRow
        {
            public Guid Id { get; set; }
            public Guid ChatServerId { get; set; }
            public Guid InvitedBy { get; set; }
            public Guid UserId { get; set; }
            public short Status { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTime ModifiedAt { get; set; }
        }
    }
}
