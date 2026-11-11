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
using HootOut.MediaFiles.Entities;
using HootOut.Users.Entities;
using Npgsql;

namespace HootOut.PostgreSQL.IntegrationTests.Savers.ChatMessages
{
    [Collection("IntegrationTests")]
    public class ChatMessageAttachmentSaverTests : IClassFixture<PostgresTestContainer>, IClassFixture<TestS3Container>, IAsyncLifetime
    {
        private readonly IContainer container;

        private readonly ClearAllTables clearTables;

        private readonly ISaver<ChatMessageAttachment> saver;

        private readonly ISaver<ChatMessage> messageSaver;

        private readonly ISaver<MediaFile> mediaFileSaver;

        private readonly IPersistenceProvider provider;

        public ChatMessageAttachmentSaverTests(PostgresTestContainer postgresContainer)
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

            saver = container.Resolve<ISaver<ChatMessageAttachment>>();
            messageSaver = container.Resolve<ISaver<ChatMessage>>();
            mediaFileSaver = container.Resolve<ISaver<MediaFile>>();
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
        public async Task SaveManyAsync_SingleAttachment_PersistsAllColumns()
        {
            var ct = TestContext.Current.CancellationToken;
            var message = await CreateMessageAsync(ct);
            var mediaFile = await CreateMediaFileAsync(ct);
            var attachment = BuildAttachment(message.Id, mediaFile.Id);

            await saver.SaveManyAsync([attachment], ct);

            var row = await GetRowAsync(attachment.Id, ct);
            row.Should().NotBeNull();
            row.Id.Should().Be(attachment.Id);
            row.MessageId.Should().Be(message.Id);
            row.MediaFileId.Should().Be(mediaFile.Id);
            row.Deleted.Should().BeFalse();
            // timestamptz stores microseconds while DateTime has 100 ns ticks
            row.CreatedAt.Should().BeCloseTo(attachment.CreatedAt, TimeSpan.FromMilliseconds(1));
            row.ModifiedAt.Should().BeCloseTo(attachment.ModifiedAt, TimeSpan.FromMilliseconds(1));
        }

        [Fact]
        public async Task SaveManyAsync_ManyAttachments_PersistsEveryRow()
        {
            var ct = TestContext.Current.CancellationToken;
            var message = await CreateMessageAsync(ct);
            var attachments = new List<ChatMessageAttachment>();
            for (var i = 0; i < 3; i++)
            {
                attachments.Add(BuildAttachment(message.Id, (await CreateMediaFileAsync(ct)).Id));
            }

            await saver.SaveManyAsync(attachments, ct);

            var ids = await GetIdsByMessageAsync(message.Id, ct);
            ids.Should().BeEquivalentTo(attachments.Select(a => a.Id));
        }

        [Fact]
        public async Task SaveManyAsync_CalledTwiceForSameMessage_KeepsBothBatches()
        {
            var ct = TestContext.Current.CancellationToken;
            var message = await CreateMessageAsync(ct);
            var first = BuildAttachment(message.Id, (await CreateMediaFileAsync(ct)).Id);
            var second = BuildAttachment(message.Id, (await CreateMediaFileAsync(ct)).Id);

            await saver.SaveManyAsync([first], ct);
            await saver.SaveManyAsync([second], ct);

            var ids = await GetIdsByMessageAsync(message.Id, ct);
            ids.Should().BeEquivalentTo([first.Id, second.Id]);
        }

        [Fact]
        public async Task SaveManyAsync_EmptyList_DoesNothing()
        {
            var ct = TestContext.Current.CancellationToken;

            var act = () => saver.SaveManyAsync([], ct);

            await act.Should().NotThrowAsync();
            (await CountRowsAsync(ct)).Should().Be(0);
        }

        [Fact]
        public async Task SaveManyAsync_Null_DoesNothing()
        {
            var ct = TestContext.Current.CancellationToken;

            var act = () => saver.SaveManyAsync(null!, ct);

            await act.Should().NotThrowAsync();
            (await CountRowsAsync(ct)).Should().Be(0);
        }

        [Fact]
        public async Task SaveManyAsync_NotExistingMessage_ThrowsWrappedForeignKeyViolation()
        {
            var ct = TestContext.Current.CancellationToken;
            var missingMessageId = Guid.NewGuid();
            var attachment = BuildAttachment(missingMessageId, (await CreateMediaFileAsync(ct)).Id);

            var act = () => saver.SaveManyAsync([attachment], ct);

            var exception = await act.Should().ThrowExactlyAsync<Exception>()
                .WithMessage($"*{missingMessageId}*");
            exception.WithInnerException<PostgresException>()
                .Which.ConstraintName.Should().Be("fk_chat_message_attachment_message");
            (await CountRowsAsync(ct)).Should().Be(0);
        }

        [Fact]
        public async Task SaveManyAsync_NotExistingMediaFile_ThrowsWrappedForeignKeyViolation()
        {
            var ct = TestContext.Current.CancellationToken;
            var message = await CreateMessageAsync(ct);
            var attachment = BuildAttachment(message.Id, Guid.NewGuid());

            var act = () => saver.SaveManyAsync([attachment], ct);

            var exception = await act.Should().ThrowExactlyAsync<Exception>()
                .WithMessage($"*{message.Id}*");
            exception.WithInnerException<PostgresException>()
                .Which.ConstraintName.Should().Be("fk_chat_message_attachment_media_file");
            (await CountRowsAsync(ct)).Should().Be(0);
        }

        [Fact]
        public async Task SaveManyAsync_OneInvalidItemInBatch_RollsBackWholeBatch()
        {
            var ct = TestContext.Current.CancellationToken;
            var message = await CreateMessageAsync(ct);
            var valid = BuildAttachment(message.Id, (await CreateMediaFileAsync(ct)).Id);
            var invalid = BuildAttachment(message.Id, Guid.NewGuid());
            var alsoValid = BuildAttachment(message.Id, (await CreateMediaFileAsync(ct)).Id);

            var act = () => saver.SaveManyAsync([valid, invalid, alsoValid], ct);

            var exception = await act.Should().ThrowExactlyAsync<Exception>();
            exception.WithInnerException<PostgresException>()
                .Which.SqlState.Should().Be(PostgresErrorCodes.ForeignKeyViolation);
            // The first row was inserted before the failure, but the transaction is never committed
            (await CountRowsAsync(ct)).Should().Be(0);
        }

        [Fact]
        public async Task SaveManyAsync_DuplicateIdInBatch_ThrowsWrappedUniqueViolationAndRollsBack()
        {
            var ct = TestContext.Current.CancellationToken;
            var message = await CreateMessageAsync(ct);
            var first = BuildAttachment(message.Id, (await CreateMediaFileAsync(ct)).Id);
            var duplicate = BuildAttachment(message.Id, (await CreateMediaFileAsync(ct)).Id);
            duplicate.Id = first.Id;

            var act = () => saver.SaveManyAsync([first, duplicate], ct);

            var exception = await act.Should().ThrowExactlyAsync<Exception>();
            exception.WithInnerException<PostgresException>()
                .Which.SqlState.Should().Be(PostgresErrorCodes.UniqueViolation);
            (await CountRowsAsync(ct)).Should().Be(0);
        }

        [Fact]
        public async Task SaveManyAsync_IdAlreadyStored_ThrowsWrappedUniqueViolationAndKeepsOriginal()
        {
            var ct = TestContext.Current.CancellationToken;
            var message = await CreateMessageAsync(ct);
            var original = BuildAttachment(message.Id, (await CreateMediaFileAsync(ct)).Id);
            await saver.SaveManyAsync([original], ct);

            var duplicate = BuildAttachment(message.Id, (await CreateMediaFileAsync(ct)).Id);
            duplicate.Id = original.Id;

            var act = () => saver.SaveManyAsync([duplicate], ct);

            var exception = await act.Should().ThrowExactlyAsync<Exception>();
            exception.WithInnerException<PostgresException>()
                .Which.SqlState.Should().Be(PostgresErrorCodes.UniqueViolation);
            var row = await GetRowAsync(original.Id, ct);
            row!.MediaFileId.Should().Be(original.MediaFileId);
            (await CountRowsAsync(ct)).Should().Be(1);
        }

        [Fact]
        public async Task SaveManyAsync_CancelledToken_ThrowsOperationCanceledAndDoesNotPersist()
        {
            var message = await CreateMessageAsync(TestContext.Current.CancellationToken);
            var mediaFile = await CreateMediaFileAsync(TestContext.Current.CancellationToken);
            var attachment = BuildAttachment(message.Id, mediaFile.Id);
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            var act = () => saver.SaveManyAsync([attachment], cts.Token);

            // The token is already cancelled when opening the connection, which happens outside the try/catch
            await act.Should().ThrowAsync<OperationCanceledException>();
            (await CountRowsAsync(TestContext.Current.CancellationToken)).Should().Be(0);
        }

        private static ChatMessageAttachment BuildAttachment(Guid messageId, Guid mediaFileId)
        {
            var attachment = Persistable.CreateNew<ChatMessageAttachment>();
            attachment.MessageId = messageId;
            attachment.MediaFileId = mediaFileId;
            return attachment;
        }

        private async Task<ChatMessage> CreateMessageAsync(CancellationToken ct)
        {
            // ChatMessage has foreign keys to UserInfo and ChatChannel, and ChatChannel to ChatServer
            var author = Persistable.CreateNew<UserInfo>();
            author.Username = $"user-{author.Id}";
            author.Email = $"{author.Id}@test.com";
            author.Password = "hash";
            await container.Resolve<ISaver<UserInfo>>().SaveAsync(author, ct);

            var server = Persistable.CreateNew<ChatServer>();
            server.Name = $"server-{server.Id}";
            server.Description = "Test server";
            server.OwnerId = author.Id;
            await container.Resolve<ISaver<ChatServer>>().SaveAsync(server, ct);

            var channel = Persistable.CreateNew<ChatChannel>();
            channel.ChatServerId = server.Id;
            channel.Name = "general";
            await container.Resolve<ISaver<ChatChannel>>().SaveAsync(channel, ct);

            var message = Persistable.CreateNew<ChatMessage>();
            message.AuthorId = author.Id;
            message.ChatChannelId = channel.Id;
            message.Content = "message with attachments";
            await messageSaver.SaveAsync(message, ct);
            return message;
        }

        private async Task<MediaFile> CreateMediaFileAsync(CancellationToken ct)
        {
            var mediaFile = Persistable.CreateNew<MediaFile>();
            mediaFile.FileName = $"{mediaFile.Id}.png";
            mediaFile.Checksum = Guid.NewGuid().ToString("N");
            mediaFile.ContentType = "image/png";
            mediaFile.BytesSize = 1024;
            mediaFile.Url = $"http://localhost/media/{mediaFile.FileName}";
            mediaFile.OwnerId = Guid.NewGuid();
            await mediaFileSaver.SaveAsync(mediaFile, ct);
            return mediaFile;
        }

        private async Task<AttachmentRow?> GetRowAsync(Guid id, CancellationToken ct)
        {
            await using var conn = await provider.OpenConnectionAsync(ct);
            return await conn.QuerySingleOrDefaultAsync<AttachmentRow>(new CommandDefinition(
                """
                    SELECT "Id", "MessageId", "MediaFileId", "Deleted", "CreatedAt", "ModifiedAt"
                    FROM "HootOut"."ChatMessageAttachment"
                    WHERE "Id" = @Id
                """,
                new { Id = id },
                cancellationToken: ct));
        }

        private async Task<IEnumerable<Guid>> GetIdsByMessageAsync(Guid messageId, CancellationToken ct)
        {
            await using var conn = await provider.OpenConnectionAsync(ct);
            return await conn.QueryAsync<Guid>(new CommandDefinition(
                """SELECT "Id" FROM "HootOut"."ChatMessageAttachment" WHERE "MessageId" = @MessageId""",
                new { MessageId = messageId },
                cancellationToken: ct));
        }

        private async Task<long> CountRowsAsync(CancellationToken ct)
        {
            await using var conn = await provider.OpenConnectionAsync(ct);
            return await conn.ExecuteScalarAsync<long>(new CommandDefinition(
                """SELECT COUNT(*) FROM "HootOut"."ChatMessageAttachment" """,
                cancellationToken: ct));
        }

        /// <summary>
        /// Raw table row, including the Deleted column.
        /// </summary>
        private sealed class AttachmentRow
        {
            public Guid Id { get; set; }
            public Guid MessageId { get; set; }
            public Guid MediaFileId { get; set; }
            public bool Deleted { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTime ModifiedAt { get; set; }
        }
    }
}
