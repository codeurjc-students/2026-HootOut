using Autofac;
using Dapper;
using FluentAssertions;
using HootOut.ChatChannels.Entities;
using HootOut.ChatMessages.Entities;
using HootOut.ChatMessages.Repositories;
using HootOut.ChatServers.Entities;
using HootOut.CommonDomain.DefaultValues;
using HootOut.CommonDomain.Persistence;
using HootOut.CommonIntegrationTests.PostgreSQL;
using HootOut.CommonIntegrationTests.S3;
using HootOut.Contracts.Common.Saver;
using HootOut.Infraestructure.DI;
using HootOut.MediaFiles.Entities;
using HootOut.Users.Entities;

namespace HootOut.PostgreSQL.IntegrationTests.Repositories.ChatMessages
{
    [Collection("IntegrationTests")]
    public class ChatMessageAttachmentRepositoryTests : IClassFixture<PostgresTestContainer>, IClassFixture<TestS3Container>, IAsyncLifetime
    {
        private static readonly DateTime BaseDate = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        private readonly IContainer container;

        private readonly ClearAllTables clearTables;

        private readonly IChatMessageAttachmentRepository repository;

        private readonly ISaver<ChatMessage> messageSaver;

        private readonly ISaver<MediaFile> mediaFileSaver;

        private readonly ISaver<ChatMessageAttachment> attachmentSaver;

        private readonly IPersistenceProvider provider;

        public ChatMessageAttachmentRepositoryTests(PostgresTestContainer postgresContainer)
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

            repository = container.Resolve<IChatMessageAttachmentRepository>();
            messageSaver = container.Resolve<ISaver<ChatMessage>>();
            mediaFileSaver = container.Resolve<ISaver<MediaFile>>();
            attachmentSaver = container.Resolve<ISaver<ChatMessageAttachment>>();
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
        public async Task GetChatMessageAttachmentDtos_MessageWithoutAttachments_ReturnsEmpty()
        {
            var ct = TestContext.Current.CancellationToken;
            var message = await CreateMessageAsync(ct);

            var result = await repository.GetChatMessageAttachmentDtos(message.Id, ct);

            result.Should().BeEmpty();
        }

        [Fact]
        public async Task GetChatMessageAttachmentDtos_NotExistingMessage_ReturnsEmpty()
        {
            var ct = TestContext.Current.CancellationToken;
            var message = await CreateMessageAsync(ct);
            var mediaFile = await CreateMediaFileAsync("photo.png", "image/png", ct);
            await CreateAttachmentAsync(message.Id, mediaFile.Id, BaseDate, ct);

            var result = await repository.GetChatMessageAttachmentDtos(Guid.NewGuid(), ct);

            result.Should().BeEmpty();
        }

        [Fact]
        public async Task GetChatMessageAttachmentDtos_Existing_ReturnsMappedDto()
        {
            var ct = TestContext.Current.CancellationToken;
            var message = await CreateMessageAsync(ct);
            var mediaFile = await CreateMediaFileAsync("report.pdf", "application/pdf", ct);
            var attachment = await CreateAttachmentAsync(message.Id, mediaFile.Id, BaseDate, ct);

            var result = (await repository.GetChatMessageAttachmentDtos(message.Id, ct)).ToList();

            result.Should().ContainSingle();
            var dto = result[0];
            dto.Id.Should().Be(attachment.Id);
            dto.MessageId.Should().Be(message.Id);
            dto.MediaFileId.Should().Be(mediaFile.Id);
            dto.FileName.Should().Be("report.pdf");
            dto.ContentType.Should().Be("application/pdf");
            // Pre-signed S3 urls are filled in by the service, not by the repository
            dto.Url.Should().BeEmpty();
            dto.DownloadUrl.Should().BeEmpty();
        }

        [Fact]
        public async Task GetChatMessageAttachmentDtos_ManyMessages_ReturnsOnlyAttachmentsOfRequestedMessage()
        {
            var ct = TestContext.Current.CancellationToken;
            var message = await CreateMessageAsync(ct);
            var otherMessage = await CreateMessageAsync(ct);
            var first = await CreateAttachmentAsync(message.Id, (await CreateMediaFileAsync("1.png", "image/png", ct)).Id, BaseDate, ct);
            var second = await CreateAttachmentAsync(message.Id, (await CreateMediaFileAsync("2.png", "image/png", ct)).Id, BaseDate.AddMinutes(1), ct);
            await CreateAttachmentAsync(otherMessage.Id, (await CreateMediaFileAsync("3.png", "image/png", ct)).Id, BaseDate, ct);

            var result = await repository.GetChatMessageAttachmentDtos(message.Id, ct);

            result.Select(a => a.Id).Should().BeEquivalentTo([first.Id, second.Id]);
            result.Should().OnlyContain(a => a.MessageId == message.Id);
        }

        [Fact]
        public async Task GetChatMessageAttachmentDtos_SameMediaFileInTwoMessages_ReturnsItForEachMessage()
        {
            var ct = TestContext.Current.CancellationToken;
            var message = await CreateMessageAsync(ct);
            var otherMessage = await CreateMessageAsync(ct);
            var mediaFile = await CreateMediaFileAsync("shared.png", "image/png", ct);
            var attachment = await CreateAttachmentAsync(message.Id, mediaFile.Id, BaseDate, ct);
            var otherAttachment = await CreateAttachmentAsync(otherMessage.Id, mediaFile.Id, BaseDate, ct);

            var result = await repository.GetChatMessageAttachmentDtos(message.Id, ct);
            var otherResult = await repository.GetChatMessageAttachmentDtos(otherMessage.Id, ct);

            result.Select(a => a.Id).Should().Equal(attachment.Id);
            otherResult.Select(a => a.Id).Should().Equal(otherAttachment.Id);
        }

        [Fact]
        public async Task GetChatMessageAttachmentDtos_DeletedAttachment_IsExcluded()
        {
            var ct = TestContext.Current.CancellationToken;
            var message = await CreateMessageAsync(ct);
            var kept = await CreateAttachmentAsync(message.Id, (await CreateMediaFileAsync("kept.png", "image/png", ct)).Id, BaseDate, ct);
            var deleted = await CreateAttachmentAsync(message.Id, (await CreateMediaFileAsync("deleted.png", "image/png", ct)).Id, BaseDate.AddMinutes(1), ct);
            await MarkAsDeletedAsync("ChatMessageAttachment", deleted.Id, ct);

            var result = await repository.GetChatMessageAttachmentDtos(message.Id, ct);

            result.Select(a => a.Id).Should().Equal(kept.Id);
        }

        [Fact]
        public async Task GetChatMessageAttachmentDtos_DeletedMediaFile_IsExcluded()
        {
            var ct = TestContext.Current.CancellationToken;
            var message = await CreateMessageAsync(ct);
            var kept = await CreateAttachmentAsync(message.Id, (await CreateMediaFileAsync("kept.png", "image/png", ct)).Id, BaseDate, ct);
            var deletedFile = await CreateMediaFileAsync("deleted.png", "image/png", ct);
            await CreateAttachmentAsync(message.Id, deletedFile.Id, BaseDate.AddMinutes(1), ct);
            await MarkAsDeletedAsync("MediaFile", deletedFile.Id, ct);

            var result = await repository.GetChatMessageAttachmentDtos(message.Id, ct);

            result.Select(a => a.Id).Should().Equal(kept.Id);
        }

        [Fact]
        public async Task GetChatMessageAttachmentDtos_AllDeleted_ReturnsEmpty()
        {
            var ct = TestContext.Current.CancellationToken;
            var message = await CreateMessageAsync(ct);
            var attachment = await CreateAttachmentAsync(message.Id, (await CreateMediaFileAsync("photo.png", "image/png", ct)).Id, BaseDate, ct);
            await MarkAsDeletedAsync("ChatMessageAttachment", attachment.Id, ct);

            var result = await repository.GetChatMessageAttachmentDtos(message.Id, ct);

            result.Should().BeEmpty();
        }

        [Fact]
        public async Task GetChatMessageAttachmentDtos_DifferentCreatedAt_OrderedByCreatedAt()
        {
            var ct = TestContext.Current.CancellationToken;
            var message = await CreateMessageAsync(ct);
            // Inserted out of order so the result does not depend on insertion order
            var third = await CreateAttachmentAsync(message.Id, (await CreateMediaFileAsync("3.png", "image/png", ct)).Id, BaseDate.AddMinutes(2), ct);
            var first = await CreateAttachmentAsync(message.Id, (await CreateMediaFileAsync("1.png", "image/png", ct)).Id, BaseDate, ct);
            var second = await CreateAttachmentAsync(message.Id, (await CreateMediaFileAsync("2.png", "image/png", ct)).Id, BaseDate.AddMinutes(1), ct);

            var result = await repository.GetChatMessageAttachmentDtos(message.Id, ct);

            result.Select(a => a.Id).Should().Equal(first.Id, second.Id, third.Id);
        }

        [Fact]
        public async Task GetChatMessageAttachmentDtos_SameCreatedAt_OrderedById()
        {
            var ct = TestContext.Current.CancellationToken;
            var message = await CreateMessageAsync(ct);
            var attachments = new List<ChatMessageAttachment>();
            for (var i = 0; i < 5; i++)
            {
                var mediaFile = await CreateMediaFileAsync($"{i}.png", "image/png", ct);
                attachments.Add(await CreateAttachmentAsync(message.Id, mediaFile.Id, BaseDate, ct));
            }

            var result = await repository.GetChatMessageAttachmentDtos(message.Id, ct);

            // PostgreSQL compares uuids byte by byte, which matches the order of their lowercase string form
            var expected = attachments.Select(a => a.Id).OrderBy(id => id.ToString(), StringComparer.Ordinal);
            result.Select(a => a.Id).Should().Equal(expected);
        }

        [Fact]
        public async Task GetChatMessageAttachmentDtos_CalledTwice_ReturnsSameOrder()
        {
            var ct = TestContext.Current.CancellationToken;
            var message = await CreateMessageAsync(ct);
            for (var i = 0; i < 5; i++)
            {
                var mediaFile = await CreateMediaFileAsync($"{i}.png", "image/png", ct);
                await CreateAttachmentAsync(message.Id, mediaFile.Id, BaseDate.AddMinutes(i % 2), ct);
            }

            var first = await repository.GetChatMessageAttachmentDtos(message.Id, ct);
            var second = await repository.GetChatMessageAttachmentDtos(message.Id, ct);

            second.Select(a => a.Id).Should().Equal(first.Select(a => a.Id));
        }

        [Fact]
        public async Task GetChatMessageAttachmentDtos_CancelledToken_ThrowsOperationCanceled()
        {
            var message = await CreateMessageAsync(TestContext.Current.CancellationToken);
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            var act = () => repository.GetChatMessageAttachmentDtos(message.Id, cts.Token);

            await act.Should().ThrowAsync<OperationCanceledException>();
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

        private async Task<MediaFile> CreateMediaFileAsync(string fileName, string contentType, CancellationToken ct)
        {
            var mediaFile = Persistable.CreateNew<MediaFile>();
            mediaFile.FileName = fileName;
            mediaFile.Checksum = Guid.NewGuid().ToString("N");
            mediaFile.ContentType = contentType;
            mediaFile.BytesSize = 1024;
            mediaFile.Url = $"http://localhost/media/{fileName}";
            mediaFile.OwnerId = Guid.NewGuid();
            await mediaFileSaver.SaveAsync(mediaFile, ct);
            return mediaFile;
        }

        private async Task<ChatMessageAttachment> CreateAttachmentAsync(Guid messageId, Guid mediaFileId, DateTime createdAt, CancellationToken ct)
        {
            var attachment = new ChatMessageAttachment
            {
                Id = Guid.CreateVersion7(),
                MessageId = messageId,
                MediaFileId = mediaFileId,
                CreatedAt = createdAt,
                ModifiedAt = createdAt
            };
            await attachmentSaver.SaveManyAsync([attachment], ct);
            return attachment;
        }

        private async Task MarkAsDeletedAsync(string table, Guid id, CancellationToken ct)
        {
            await using var conn = await provider.OpenConnectionAsync(ct);
            await conn.ExecuteAsync(new CommandDefinition(
                $"""UPDATE "HootOut"."{table}" SET "Deleted" = true WHERE "Id" = @Id""",
                new { Id = id },
                cancellationToken: ct));
        }
    }
}
