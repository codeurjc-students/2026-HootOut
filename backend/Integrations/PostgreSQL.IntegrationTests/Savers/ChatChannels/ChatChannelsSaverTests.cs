using Autofac;
using Dapper;
using FluentAssertions;
using HootOut.ChatChannels.Entities;
using HootOut.ChatServers.Entities;
using HootOut.CommonDomain.DefaultValues;
using HootOut.CommonDomain.Persistence;
using HootOut.CommonIntegrationTests.PostgreSQL;
using HootOut.CommonIntegrationTests.S3;
using HootOut.Contracts.ChatConversations.Enums;
using HootOut.Contracts.Common.Saver;
using HootOut.Infraestructure.DI;
using HootOut.Users.Entities;
using Npgsql;

namespace HootOut.PostgreSQL.IntegrationTests.Savers.ChatChannels
{
    [Collection("IntegrationTests")]
    public class ChatChannelsSaverTests : IClassFixture<PostgresTestContainer>, IClassFixture<TestS3Container>, IAsyncLifetime
    {
        private readonly IContainer container;

        private readonly ClearAllTables clearTables;

        private UserInfo owner = null!;

        private readonly ISaver<UserInfo> userSaver;

        private readonly ISaver<ChatChannel> saver;

        private readonly ISaver<ChatServer> serverSaver;

        private readonly IPersistenceProvider provider;

        public ChatChannelsSaverTests(PostgresTestContainer postgresContainer)
        {
            var builder = new ContainerBuilder();
            new RegistrationManager().RegisterAllAssemblies(builder);
            builder.RegisterInstance(postgresContainer.postgreSQLProvider!).As<IPersistenceProvider>().SingleInstance();
            container = builder.Build();

            provider = container.Resolve<IPersistenceProvider>();
            Assert.Same(postgresContainer.postgreSQLProvider, provider);

            clearTables = container.Resolve<ClearAllTables>();
            userSaver = container.Resolve<ISaver<UserInfo>>();
            var defaultValues = container.Resolve<IEnumerable<IDefaultValues>>().OrderBy(x => x.Priority);

            foreach (var value in defaultValues)
            {
                value.InitAsync().Wait();
            }

            saver = container.Resolve<ISaver<ChatChannel>>();
            serverSaver = container.Resolve<ISaver<ChatServer>>();
        }

        public async ValueTask InitializeAsync()
        {
            await clearTables.ClearTablesAsync();
            owner = await CreateOwnerAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await clearTables.ClearTablesAsync();
            GC.SuppressFinalize(this);
        }

        #region SaveAsync

        [Fact]
        public async Task SaveAsync_NewChannel_PersistsAllColumns()
        {
            var ct = TestContext.Current.CancellationToken;
            var server = await CreateServerAsync(ct);
            var channel = BuildChannel(server.Id, "general");

            await saver.SaveAsync(channel, ct);

            var row = await GetRowAsync(channel.Id, ct);
            row.Should().NotBeNull();
            row.Id.Should().Be(channel.Id);
            row.ChatServerId.Should().Be(server.Id);
            row.ChatConversationId.Should().NotBeEmpty().And.Be(channel.ChatConversationId);
            row.Name.Should().Be("general");
            row.Deleted.Should().BeFalse();
            // timestamptz stores microseconds while DateTime has 100 ns ticks
            row.CreatedAt.Should().BeCloseTo(channel.CreatedAt, TimeSpan.FromMilliseconds(1));
            row.ModifiedAt.Should().BeCloseTo(channel.ModifiedAt, TimeSpan.FromMilliseconds(1));
        }

        [Fact]
        public async Task SaveAsync_NewChannel_CreatesChannelConversation()
        {
            var ct = TestContext.Current.CancellationToken;
            var server = await CreateServerAsync(ct);
            var channel = BuildChannel(server.Id, "general");

            await saver.SaveAsync(channel, ct);

            channel.ChatConversationId.Should().NotBeEmpty();
            var conversation = await GetConversationAsync(channel.ChatConversationId, ct);
            conversation.Should().NotBeNull();
            conversation.ConversationType.Should().Be((short)ChatConversationType.Channel);
            (await CountConversationsAsync(ct)).Should().Be(1);
        }

        [Fact]
        public async Task SaveAsync_NewChannelWithPresetConversationId_UsesIt()
        {
            var ct = TestContext.Current.CancellationToken;
            var server = await CreateServerAsync(ct);
            var channel = BuildChannel(server.Id, "general");
            var conversationId = Guid.CreateVersion7();
            channel.ChatConversationId = conversationId;

            await saver.SaveAsync(channel, ct);

            channel.ChatConversationId.Should().Be(conversationId);
            (await GetRowAsync(channel.Id, ct))!.ChatConversationId.Should().Be(conversationId);
            (await GetConversationAsync(conversationId, ct)).Should().NotBeNull();
        }

        [Fact]
        public async Task SaveAsync_SeveralChannels_EachGetsItsOwnConversation()
        {
            var ct = TestContext.Current.CancellationToken;
            var server = await CreateServerAsync(ct);
            var first = BuildChannel(server.Id, "general");
            var second = BuildChannel(server.Id, "random");

            await saver.SaveAsync(first, ct);
            await saver.SaveAsync(second, ct);

            first.ChatConversationId.Should().NotBe(second.ChatConversationId);
            (await CountConversationsAsync(ct)).Should().Be(2);
        }

        [Fact]
        public async Task SaveAsync_ExistingChannelWithoutConversationId_KeepsItsConversation()
        {
            var ct = TestContext.Current.CancellationToken;
            var server = await CreateServerAsync(ct);
            var original = BuildChannel(server.Id, "general");
            await saver.SaveAsync(original, ct);

            // Same channel built again without the conversation, e.g. from a request body
            var updated = BuildChannel(server.Id, "renamed");
            updated.Id = original.Id;

            await saver.SaveAsync(updated, ct);

            // RETURNING gives back the conversation the channel already had
            updated.ChatConversationId.Should().Be(original.ChatConversationId);
            (await GetRowAsync(original.Id, ct))!.ChatConversationId.Should().Be(original.ChatConversationId);
            (await CountConversationsAsync(ct)).Should().Be(1);
        }

        [Fact]
        public async Task SaveAsync_RepeatedSaves_DoNotCreateOrphanConversations()
        {
            var ct = TestContext.Current.CancellationToken;
            var server = await CreateServerAsync(ct);
            var channel = BuildChannel(server.Id, "general");

            for (var i = 0; i < 3; i++)
            {
                channel.Name = $"name-{i}";
                await saver.SaveAsync(channel, ct);
            }

            (await CountConversationsAsync(ct)).Should().Be(1);
            (await GetRowAsync(channel.Id, ct))!.Name.Should().Be("name-2");
        }

        [Fact]
        public async Task SaveAsync_ConversationOfAnotherChannel_ThrowsWrappedUniqueViolation()
        {
            var ct = TestContext.Current.CancellationToken;
            var server = await CreateServerAsync(ct);
            var existing = BuildChannel(server.Id, "general");
            await saver.SaveAsync(existing, ct);
            var channel = BuildChannel(server.Id, "random");
            channel.ChatConversationId = existing.ChatConversationId;

            var act = () => saver.SaveAsync(channel, ct);

            var exception = await act.Should().ThrowExactlyAsync<Exception>()
                .WithMessage($"*{channel.Id}*");
            exception.WithInnerException<PostgresException>()
                .Which.ConstraintName.Should().Be("ux_chat_channel_conversation");
            (await CountRowsAsync(ct)).Should().Be(1);
        }

        [Fact]
        public async Task SaveAsync_DuplicatedNameInSameServer_PersistsBoth()
        {
            var ct = TestContext.Current.CancellationToken;
            var server = await CreateServerAsync(ct);

            await saver.SaveAsync(BuildChannel(server.Id, "general"), ct);
            await saver.SaveAsync(BuildChannel(server.Id, "general"), ct);

            (await CountRowsAsync(ct)).Should().Be(2);
        }

        [Fact]
        public async Task SaveAsync_ExistingChannel_UpdatesNameAndModifiedAtOnly()
        {
            var ct = TestContext.Current.CancellationToken;
            var server = await CreateServerAsync(ct);
            var otherServer = await CreateServerAsync(ct);
            var original = BuildChannel(server.Id, "general");
            await saver.SaveAsync(original, ct);

            var updated = new ChatChannel
            {
                Id = original.Id,
                ChatServerId = otherServer.Id,
                ChatConversationId = original.ChatConversationId,
                Name = "renamed",
                CreatedAt = original.CreatedAt.AddDays(-10),
                ModifiedAt = original.ModifiedAt.AddDays(1)
            };

            await saver.SaveAsync(updated, ct);

            var row = await GetRowAsync(original.Id, ct);
            row.Should().NotBeNull();
            row.Name.Should().Be("renamed");
            row.ModifiedAt.Should().BeCloseTo(updated.ModifiedAt, TimeSpan.FromMilliseconds(1));
            // A channel always belongs to the server where it was created
            row.ChatServerId.Should().Be(server.Id);
            row.CreatedAt.Should().BeCloseTo(original.CreatedAt, TimeSpan.FromMilliseconds(1));
            row.ChatConversationId.Should().Be(original.ChatConversationId);
            (await CountRowsAsync(ct)).Should().Be(1);
            (await CountConversationsAsync(ct)).Should().Be(1);
        }

        [Fact]
        public async Task SaveAsync_DeletedChannel_DoesNotRestoreIt()
        {
            var ct = TestContext.Current.CancellationToken;
            var server = await CreateServerAsync(ct);
            var channel = BuildChannel(server.Id, "general");
            await saver.SaveAsync(channel, ct);
            await saver.DeleteAsync(channel, ct);

            channel.Name = "renamed";
            await saver.SaveAsync(channel, ct);

            var row = await GetRowAsync(channel.Id, ct);
            row!.Deleted.Should().BeTrue();
        }

        [Fact]
        public async Task SaveAsync_NotExistingServer_ThrowsWrappedForeignKeyViolation()
        {
            var ct = TestContext.Current.CancellationToken;
            var channel = BuildChannel(Guid.NewGuid(), "general");

            var act = () => saver.SaveAsync(channel, ct);

            var exception = await act.Should().ThrowExactlyAsync<Exception>()
                .WithMessage($"*{channel.Id}*");
            exception.WithInnerException<PostgresException>()
                .Which.SqlState.Should().Be(PostgresErrorCodes.ForeignKeyViolation);
            (await CountRowsAsync(ct)).Should().Be(0);
            // The conversation is inserted in the same statement, so it is rolled back too
            (await CountConversationsAsync(ct)).Should().Be(0);
        }

        [Fact]
        public async Task SaveAsync_LongName_PersistsWithoutTruncation()
        {
            var ct = TestContext.Current.CancellationToken;
            var server = await CreateServerAsync(ct);
            // "Name" is TEXT, so there is no length limit in the database
            var channel = BuildChannel(server.Id, new string('a', 1000));

            await saver.SaveAsync(channel, ct);

            var row = await GetRowAsync(channel.Id, ct);
            row!.Name.Should().HaveLength(1000);
        }

        [Fact]
        public async Task SaveAsync_CancelledToken_ThrowsOperationCanceledAndDoesNotPersist()
        {
            var server = await CreateServerAsync(TestContext.Current.CancellationToken);
            var channel = BuildChannel(server.Id, "general");
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            var act = () => saver.SaveAsync(channel, cts.Token);

            // The token is already cancelled when opening the connection, which happens outside the try/catch
            await act.Should().ThrowAsync<OperationCanceledException>();
            (await CountRowsAsync(TestContext.Current.CancellationToken)).Should().Be(0);
            (await CountConversationsAsync(TestContext.Current.CancellationToken)).Should().Be(0);
        }

        #endregion

        #region DeleteAsync

        [Fact]
        public async Task DeleteAsync_ExistingChannel_MarksAsDeletedAndUpdatesModifiedAt()
        {
            var ct = TestContext.Current.CancellationToken;
            var server = await CreateServerAsync(ct);
            var channel = BuildChannel(server.Id, "general");
            // Older dates so the new ModifiedAt is clearly later
            channel.CreatedAt = channel.CreatedAt.AddDays(-1);
            channel.ModifiedAt = channel.CreatedAt;
            await saver.SaveAsync(channel, ct);

            var before = DateTime.UtcNow;
            await saver.DeleteAsync(channel, ct);

            var row = await GetRowAsync(channel.Id, ct);
            row.Should().NotBeNull();
            row.Deleted.Should().BeTrue();
            row.ModifiedAt.Should().BeOnOrAfter(before.AddMilliseconds(-1));
            row.Name.Should().Be("general");
            row.ChatServerId.Should().Be(server.Id);
            row.CreatedAt.Should().BeCloseTo(channel.CreatedAt, TimeSpan.FromMilliseconds(1));
        }

        [Fact]
        public async Task DeleteAsync_SeveralChannels_OnlyDeletesRequestedOne()
        {
            var ct = TestContext.Current.CancellationToken;
            var server = await CreateServerAsync(ct);
            var deleted = BuildChannel(server.Id, "general");
            var kept = BuildChannel(server.Id, "random");
            await saver.SaveAsync(deleted, ct);
            await saver.SaveAsync(kept, ct);

            await saver.DeleteAsync(deleted, ct);

            (await GetRowAsync(deleted.Id, ct))!.Deleted.Should().BeTrue();
            (await GetRowAsync(kept.Id, ct))!.Deleted.Should().BeFalse();
        }

        [Fact]
        public async Task DeleteAsync_NotExistingChannel_DoesNotThrow()
        {
            var ct = TestContext.Current.CancellationToken;
            var channel = BuildChannel(Guid.NewGuid(), "general");

            var act = () => saver.DeleteAsync(channel, ct);

            await act.Should().NotThrowAsync();
            (await CountRowsAsync(ct)).Should().Be(0);
        }

        [Fact]
        public async Task DeleteAsync_CancelledToken_ThrowsOperationCanceledAndDoesNotDelete()
        {
            var server = await CreateServerAsync(TestContext.Current.CancellationToken);
            var channel = BuildChannel(server.Id, "general");
            await saver.SaveAsync(channel, TestContext.Current.CancellationToken);
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            var act = () => saver.DeleteAsync(channel, cts.Token);

            await act.Should().ThrowAsync<OperationCanceledException>();
            (await GetRowAsync(channel.Id, TestContext.Current.CancellationToken))!.Deleted.Should().BeFalse();
        }

        #endregion

        private async Task<ChatServer> CreateServerAsync(CancellationToken ct)
        {
            var server = Persistable.CreateNew<ChatServer>();
            server.Name = $"server-{server.Id}";
            server.Description = "Test server";
            server.OwnerId = owner.Id;
            await serverSaver.SaveAsync(server, ct);
            return server;
        }

        private static ChatChannel BuildChannel(Guid serverId, string name)
        {
            var channel = Persistable.CreateNew<ChatChannel>();
            channel.ChatServerId = serverId;
            channel.Name = name;
            return channel;
        }

        private async Task<ChatChannelRow?> GetRowAsync(Guid id, CancellationToken ct)
        {
            await using var conn = await provider.OpenConnectionAsync(ct);
            return await conn.QuerySingleOrDefaultAsync<ChatChannelRow>(new CommandDefinition(
                """
                    SELECT "Id", "ChatServerId", "ChatConversationId", "Name", "Deleted", "CreatedAt", "ModifiedAt"
                    FROM "HootOut"."ChatChannel"
                    WHERE "Id" = @Id
                """,
                new { Id = id },
                cancellationToken: ct));
        }

        private async Task<long> CountRowsAsync(CancellationToken ct)
        {
            await using var conn = await provider.OpenConnectionAsync(ct);
            return await conn.ExecuteScalarAsync<long>(new CommandDefinition(
                """SELECT COUNT(*) FROM "HootOut"."ChatChannel" """,
                cancellationToken: ct));
        }

        private async Task<ConversationRow?> GetConversationAsync(Guid id, CancellationToken ct)
        {
            await using var conn = await provider.OpenConnectionAsync(ct);
            return await conn.QuerySingleOrDefaultAsync<ConversationRow>(new CommandDefinition(
                """SELECT "Id", "ConversationType" FROM "HootOut"."ChatConversation" WHERE "Id" = @Id""",
                new { Id = id },
                cancellationToken: ct));
        }

        private async Task<long> CountConversationsAsync(CancellationToken ct)
        {
            await using var conn = await provider.OpenConnectionAsync(ct);
            return await conn.ExecuteScalarAsync<long>(new CommandDefinition(
                """SELECT COUNT(*) FROM "HootOut"."ChatConversation" """,
                cancellationToken: ct));
        }

        private sealed class ConversationRow
        {
            public Guid Id { get; set; }
            public short ConversationType { get; set; }
        }

        /// <summary>
        /// Raw table row, read without the repository filters.
        /// </summary>
        private sealed class ChatChannelRow
        {
            public Guid Id { get; set; }
            public Guid ChatServerId { get; set; }
            public Guid ChatConversationId { get; set; }
            public string Name { get; set; } = string.Empty;
            public bool Deleted { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTime ModifiedAt { get; set; }
        }

        /// <summary>
        /// ChatServer.OwnerId and MediaFile.OwnerId have a foreign key to UserInfo, so every test gets an existing owner.
        /// </summary>
        private async Task<UserInfo> CreateOwnerAsync()
        {
            var user = Persistable.CreateNew<UserInfo>();
            user.Username = $"owner-{user.Id}";
            user.Email = $"owner-{user.Id}@test.com";
            user.Password = "hash-owner";
            await userSaver.SaveAsync(user, TestContext.Current.CancellationToken);
            return user;
        }
    }
}
