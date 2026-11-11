using Autofac;
using Dapper;
using FluentAssertions;
using HootOut.ChatChannels.Entities;
using HootOut.ChatMessages.Entities;
using HootOut.ChatServers.Entities;
using HootOut.CommonDomain.DefaultValues;
using HootOut.CommonDomain.Persistence;
using HootOut.CommonIntegrationTests.PostgreSQL;
using HootOut.CommonIntegrationTests.S3;
using HootOut.Contracts.Common.Saver;
using HootOut.Infraestructure.DI;
using HootOut.Users.Entities;
using Npgsql;

namespace HootOut.PostgreSQL.IntegrationTests.Savers.ChatMessages
{
    [Collection("IntegrationTests")]
    public class ChatMessageSaverTests : IClassFixture<PostgresTestContainer>, IClassFixture<TestS3Container>, IAsyncLifetime
    {
        private readonly IContainer container;

        private readonly ClearAllTables clearTables;

        private readonly ISaver<ChatMessage> saver;

        private readonly ISaver<UserInfo> userSaver;

        private readonly ISaver<ChatServer> serverSaver;

        private readonly ISaver<ChatChannel> channelSaver;

        private readonly IPersistenceProvider provider;

        public ChatMessageSaverTests(PostgresTestContainer postgresContainer)
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

            saver = container.Resolve<ISaver<ChatMessage>>();
            userSaver = container.Resolve<ISaver<UserInfo>>();
            serverSaver = container.Resolve<ISaver<ChatServer>>();
            channelSaver = container.Resolve<ISaver<ChatChannel>>();
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
        public async Task SaveAsync_ValidMessage_PersistsAllColumns()
        {
            var ct = TestContext.Current.CancellationToken;
            var (channel, author) = await CreateChannelAsync(ct);
            var message = BuildMessage(channel.Id, author.Id, "hello world");

            await saver.SaveAsync(message, ct);

            var row = await GetRowAsync(message.Id, ct);
            row.Should().NotBeNull();
            row.Id.Should().Be(message.Id);
            row.AuthorId.Should().Be(author.Id);
            row.ChatChannelId.Should().Be(channel.Id);
            row.Content.Should().Be("hello world");
            row.Deleted.Should().BeFalse();
            // timestamptz stores microseconds while DateTime has 100 ns ticks
            row.CreatedAt.Should().BeCloseTo(message.CreatedAt, TimeSpan.FromMilliseconds(1));
            row.ModifiedAt.Should().BeCloseTo(message.ModifiedAt, TimeSpan.FromMilliseconds(1));
        }

        [Fact]
        public async Task SaveAsync_EmptyContent_Persists()
        {
            var ct = TestContext.Current.CancellationToken;
            var (channel, author) = await CreateChannelAsync(ct);
            // A message with only attachments has no text
            var message = BuildMessage(channel.Id, author.Id, string.Empty);

            await saver.SaveAsync(message, ct);

            var row = await GetRowAsync(message.Id, ct);
            row!.Content.Should().BeEmpty();
        }

        [Fact]
        public async Task SaveAsync_Many_PersistsEveryRow()
        {
            var ct = TestContext.Current.CancellationToken;
            var (channel, author) = await CreateChannelAsync(ct);

            await saver.SaveAsync(BuildMessage(channel.Id, author.Id, "1"), ct);
            await saver.SaveAsync(BuildMessage(channel.Id, author.Id, "2"), ct);
            await saver.SaveAsync(BuildMessage(channel.Id, author.Id, "3"), ct);

            (await CountRowsAsync(ct)).Should().Be(3);
        }

        [Fact]
        public async Task SaveAsync_DuplicateId_ThrowsWrappedUniqueViolationAndKeepsOriginal()
        {
            var ct = TestContext.Current.CancellationToken;
            var (channel, author) = await CreateChannelAsync(ct);
            var message = BuildMessage(channel.Id, author.Id, "original");
            await saver.SaveAsync(message, ct);

            var duplicate = BuildMessage(channel.Id, author.Id, "duplicate");
            duplicate.Id = message.Id;

            var act = () => saver.SaveAsync(duplicate, ct);

            var exception = await act.Should().ThrowExactlyAsync<Exception>()
                .WithMessage($"*{message.Id}*");
            exception.WithInnerException<PostgresException>()
                .Which.SqlState.Should().Be(PostgresErrorCodes.UniqueViolation);

            var row = await GetRowAsync(message.Id, ct);
            row!.Content.Should().Be("original");
            (await CountRowsAsync(ct)).Should().Be(1);
        }

        [Fact]
        public async Task SaveAsync_NotExistingAuthor_ThrowsWrappedForeignKeyViolation()
        {
            var ct = TestContext.Current.CancellationToken;
            var (channel, _) = await CreateChannelAsync(ct);
            var message = BuildMessage(channel.Id, Guid.NewGuid(), "orphan");

            var act = () => saver.SaveAsync(message, ct);

            var exception = await act.Should().ThrowExactlyAsync<Exception>()
                .WithMessage($"*{message.Id}*");
            exception.WithInnerException<PostgresException>()
                .Which.ConstraintName.Should().Be("fk_chat_message_author");
            (await CountRowsAsync(ct)).Should().Be(0);
        }

        [Fact]
        public async Task SaveAsync_NotExistingChannel_ThrowsWrappedForeignKeyViolation()
        {
            var ct = TestContext.Current.CancellationToken;
            var (_, author) = await CreateChannelAsync(ct);
            var message = BuildMessage(Guid.NewGuid(), author.Id, "orphan");

            var act = () => saver.SaveAsync(message, ct);

            var exception = await act.Should().ThrowExactlyAsync<Exception>()
                .WithMessage($"*{message.Id}*");
            exception.WithInnerException<PostgresException>()
                .Which.ConstraintName.Should().Be("fk_chat_message_channel");
            (await CountRowsAsync(ct)).Should().Be(0);
        }

        [Fact]
        public async Task SaveAsync_NullContent_ThrowsWrappedNotNullViolation()
        {
            var ct = TestContext.Current.CancellationToken;
            var (channel, author) = await CreateChannelAsync(ct);
            var message = BuildMessage(channel.Id, author.Id, null!);

            var act = () => saver.SaveAsync(message, ct);

            var exception = await act.Should().ThrowExactlyAsync<Exception>();
            exception.WithInnerException<PostgresException>()
                .Which.SqlState.Should().Be(PostgresErrorCodes.NotNullViolation);
            (await CountRowsAsync(ct)).Should().Be(0);
        }

        [Fact]
        public async Task SaveAsync_CancelledToken_ThrowsOperationCanceledAndDoesNotPersist()
        {
            var (channel, author) = await CreateChannelAsync(TestContext.Current.CancellationToken);
            var message = BuildMessage(channel.Id, author.Id, "cancelled");
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            var act = () => saver.SaveAsync(message, cts.Token);

            // The token is already cancelled when opening the connection, which happens outside the try/catch
            await act.Should().ThrowAsync<OperationCanceledException>();
            (await CountRowsAsync(TestContext.Current.CancellationToken)).Should().Be(0);
        }

        private static ChatMessage BuildMessage(Guid channelId, Guid authorId, string content)
        {
            var message = Persistable.CreateNew<ChatMessage>();
            message.ChatChannelId = channelId;
            message.AuthorId = authorId;
            message.Content = content;
            return message;
        }

        private async Task<(ChatChannel Channel, UserInfo Author)> CreateChannelAsync(CancellationToken ct)
        {
            var author = Persistable.CreateNew<UserInfo>();
            author.Username = $"user-{author.Id}";
            author.Email = $"{author.Id}@test.com";
            author.Password = "hash";
            await userSaver.SaveAsync(author, ct);

            var server = Persistable.CreateNew<ChatServer>();
            server.Name = $"server-{server.Id}";
            server.Description = "Test server";
            server.OwnerId = author.Id;
            await serverSaver.SaveAsync(server, ct);

            var channel = Persistable.CreateNew<ChatChannel>();
            channel.ChatServerId = server.Id;
            channel.Name = "general";
            await channelSaver.SaveAsync(channel, ct);

            return (channel, author);
        }

        private async Task<ChatMessageRow?> GetRowAsync(Guid id, CancellationToken ct)
        {
            await using var conn = await provider.OpenConnectionAsync(ct);
            return await conn.QuerySingleOrDefaultAsync<ChatMessageRow>(new CommandDefinition(
                """
                    SELECT "Id", "AuthorId", "ChatChannelId", "Content", "Deleted", "CreatedAt", "ModifiedAt"
                    FROM "HootOut"."ChatMessage"
                    WHERE "Id" = @Id
                """,
                new { Id = id },
                cancellationToken: ct));
        }

        private async Task<long> CountRowsAsync(CancellationToken ct)
        {
            await using var conn = await provider.OpenConnectionAsync(ct);
            return await conn.ExecuteScalarAsync<long>(new CommandDefinition(
                """SELECT COUNT(*) FROM "HootOut"."ChatMessage" """,
                cancellationToken: ct));
        }

        /// <summary>
        /// Raw table row, including the Deleted column.
        /// </summary>
        private sealed class ChatMessageRow
        {
            public Guid Id { get; set; }
            public Guid AuthorId { get; set; }
            public Guid ChatChannelId { get; set; }
            public string Content { get; set; } = string.Empty;
            public bool Deleted { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTime ModifiedAt { get; set; }
        }
    }
}
