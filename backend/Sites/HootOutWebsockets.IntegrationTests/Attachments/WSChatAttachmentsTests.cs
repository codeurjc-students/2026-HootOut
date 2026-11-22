using Autofac;
using FluentAssertions;
using HootOut.CommonIntegrationTests.Services;
using HootOut.Contracts.ChatMessage.Dtos;
using HootOut.Contracts.ChatMessage.Requests;
using HootOut.Contracts.Common.Saver;
using HootOut.Contracts.WebSockets.Handlers;
using HootOut.HootOutWebsockets.IntegrationTests.Common;
using HootOut.MediaFiles.Entities;

namespace HootOut.HootOutWebsockets.IntegrationTests.Attachments
{
    [Collection("IntegrationTests")]
    public class WSChatAttachmentsTests : WSTestBase, IClassFixture<WebSocketServerFixture>, IClassFixture<TestContaineRabbitConnectionFactory>
    {
        private const string EmptyMessageError = "The message must have content or attachments";
        private const string InvalidAttachmentError = "Invalid attachment";

        private readonly ISaver<MediaFile> mediaFileSaver;

        public WSChatAttachmentsTests(WebSocketServerFixture serverFixture, TestContaineRabbitConnectionFactory rabbitConnectionFactory)
            : base(serverFixture, rabbitConnectionFactory)
        {
            mediaFileSaver = container.Resolve<ISaver<MediaFile>>();
        }

        [Fact]
        public async Task DirectMessage_WithContentAndAttachments_DeliveredWithUrls()
        {
            var ct = TestContext.Current.CancellationToken;
            var userA = await CreateUserAsync("userA1");
            var userB = await CreateUserAsync("userB1");
            var (_, conversationId) = await MakeFriendsAsync(userA, userB, ct);
            var image = await CreateMediaFileAsync(userA, "foo.png", "image/png", ct);
            var document = await CreateMediaFileAsync(userA, "bar.pdf", "application/pdf", ct);

            await using var socketA = await ConnectAsync(userA, ct);
            await using var socketB = await ConnectAsync(userB, ct);
            await SubscribeAsync(socketB, conversationId, ct);

            var ack = await socketA.SendWithAckAsync(ChatMessage(conversationId, WithAttachments("look at this", image, document), Guid.NewGuid()), ct);
            var saved = ReadChatMessage(ack);
            saved.Content.Should().Be("look at this");
            AssertAttachments(saved, saved.Id, image, document);

            var received = await ExpectChatMessageAsync(socketB, conversationId, "look at this", userA, ct);
            received.Id.Should().Be(saved.Id);
            AssertAttachments(received, saved.Id, image, document);

            var messages = await GetMessagesOldestFirstAsync(conversationId, ct);
            messages.Should().ContainSingle();
            AssertAttachments(messages[0], saved.Id, image, document);
        }

        [Fact]
        public async Task DirectMessage_OnlyAttachments_Accepted()
        {
            var ct = TestContext.Current.CancellationToken;
            var userA = await CreateUserAsync("userA1");
            var userB = await CreateUserAsync("userB1");
            var (_, conversationId) = await MakeFriendsAsync(userA, userB, ct);
            var image = await CreateMediaFileAsync(userA, "foo.png", "image/png", ct);

            await using var socketA = await ConnectAsync(userA, ct);
            await using var socketB = await ConnectAsync(userB, ct);
            await SubscribeAsync(socketB, conversationId, ct);

            var ack = await socketA.SendWithAckAsync(ChatMessage(conversationId, WithAttachments("", image), Guid.NewGuid()), ct);
            var saved = ReadChatMessage(ack);
            saved.Content.Should().BeEmpty();
            AssertAttachments(saved, saved.Id, image);

            var received = await ExpectChatMessageAsync(socketB, conversationId, "", userA, ct);
            AssertAttachments(received, saved.Id, image);
        }

        [Fact]
        public async Task ServerChannel_Attachments_ReachEveryMember()
        {
            var ct = TestContext.Current.CancellationToken;
            var users = await CreateUsersAsync(3, "member");
            var (serverId, conversationId) = await CreateServerWithChannelAsync(users[0], ct);
            await AddMemberAsync(serverId, users[0], users[1], ct);
            await AddMemberAsync(serverId, users[0], users[2], ct);
            var image = await CreateMediaFileAsync(users[1], "foo.png", "image/png", ct);

            await using var socket0 = await ConnectAsync(users[0], ct);
            await using var socket1 = await ConnectAsync(users[1], ct);
            await using var socket2 = await ConnectAsync(users[2], ct);
            await SubscribeAsync(socket0, conversationId, ct);
            await SubscribeAsync(socket1, conversationId, ct);
            await SubscribeAsync(socket2, conversationId, ct);

            var ack = await socket1.SendWithAckAsync(ChatMessage(conversationId, WithAttachments("for everyone", image), Guid.NewGuid()), ct);
            var saved = ReadChatMessage(ack);

            AssertAttachments(await ExpectChatMessageAsync(socket0, conversationId, "for everyone", users[1], ct), saved.Id, image);
            AssertAttachments(await ExpectChatMessageAsync(socket2, conversationId, "for everyone", users[1], ct), saved.Id, image);
        }

        [Fact]
        public async Task SameAttachmentTwice_CreatesTwoAttachments()
        {
            var ct = TestContext.Current.CancellationToken;
            var userA = await CreateUserAsync("userA1");
            var userB = await CreateUserAsync("userB1");
            var (_, conversationId) = await MakeFriendsAsync(userA, userB, ct);
            var image = await CreateMediaFileAsync(userA, "foo.png", "image/png", ct);

            await using var socketA = await ConnectAsync(userA, ct);

            var ack = await socketA.SendWithAckAsync(ChatMessage(conversationId, WithAttachments("twice", image, image), Guid.NewGuid()), ct);
            var saved = ReadChatMessage(ack);

            saved.Attachments.Should().HaveCount(2);
            saved.Attachments.Should().OnlyContain(x => x.MediaFileId == image.Id && x.MessageId == saved.Id);
            saved.Attachments.Select(x => x.Id).Should().OnlyHaveUniqueItems();

            (await GetMessagesOldestFirstAsync(conversationId, ct)).Single().Attachments.Should().HaveCount(2);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public async Task EmptyMessageWithoutAttachments_Rejected(string content)
        {
            var ct = TestContext.Current.CancellationToken;
            var userA = await CreateUserAsync("userA1");
            var userB = await CreateUserAsync("userB1");
            var (_, conversationId) = await MakeFriendsAsync(userA, userB, ct);

            await using var socketA = await ConnectAsync(userA, ct);
            await using var socketB = await ConnectAsync(userB, ct);
            await SubscribeAsync(socketB, conversationId, ct);

            (await SendExpectingErrorAsync(socketA, ChatMessage(conversationId, content, Guid.NewGuid()), ct)).Should().Be(EmptyMessageError);

            // B did not receive anything: the next message is the valid one
            await SendChatMessageAsync(socketA, conversationId, "not empty", ct);
            await ExpectChatMessageAsync(socketB, conversationId, "not empty", userA, ct);
            (await GetMessagesOldestFirstAsync(conversationId, ct)).Select(x => x.Content).Should().Equal("not empty");
        }

        [Fact]
        public async Task NotExistingMediaFile_Rejected_NothingSaved()
        {
            var ct = TestContext.Current.CancellationToken;
            var userA = await CreateUserAsync("userA1");
            var userB = await CreateUserAsync("userB1");
            var (_, conversationId) = await MakeFriendsAsync(userA, userB, ct);

            await using var socketA = await ConnectAsync(userA, ct);
            await using var socketB = await ConnectAsync(userB, ct);
            await SubscribeAsync(socketB, conversationId, ct);

            var request = new CreateChatMessageRequest
            {
                Content = "missing file",
                Attachments = new[] { new CreateChatAttachmentRequest { MediaFileId = Guid.NewGuid() } }
            };
            (await SendExpectingErrorAsync(socketA, ChatMessage(conversationId, request, Guid.NewGuid()), ct)).Should().Be(InvalidAttachmentError);

            // Before the change the message was saved without attachments; now nothing is saved
            (await GetMessagesOldestFirstAsync(conversationId, ct)).Should().BeEmpty();
            (await socketB.NoMessageWithinAsync(TimeSpan.FromMilliseconds(300))).Should().BeTrue();
        }

        [Fact]
        public async Task MediaFileOfAnotherUser_Rejected_NothingSaved()
        {
            var ct = TestContext.Current.CancellationToken;
            var userA = await CreateUserAsync("userA1");
            var userB = await CreateUserAsync("userB1");
            var (_, conversationId) = await MakeFriendsAsync(userA, userB, ct);
            var fileOfB = await CreateMediaFileAsync(userB, "foo.png", "image/png", ct);

            await using var socketA = await ConnectAsync(userA, ct);
            await using var socketB = await ConnectAsync(userB, ct);
            await SubscribeAsync(socketB, conversationId, ct);

            (await SendExpectingErrorAsync(socketA, ChatMessage(conversationId, WithAttachments("not mine", fileOfB), Guid.NewGuid()), ct))
                .Should().Be(InvalidAttachmentError);

            (await GetMessagesOldestFirstAsync(conversationId, ct)).Should().BeEmpty();

            // The owner can send it
            var ack = await socketB.SendWithAckAsync(ChatMessage(conversationId, WithAttachments("mine", fileOfB), Guid.NewGuid()), ct);
            AssertAttachments(ReadChatMessage(ack), ReadChatMessage(ack).Id, fileOfB);
        }

        [Fact]
        public async Task ValidAndInvalidAttachments_WholeMessageRejected()
        {
            var ct = TestContext.Current.CancellationToken;
            var userA = await CreateUserAsync("userA1");
            var userB = await CreateUserAsync("userB1");
            var (_, conversationId) = await MakeFriendsAsync(userA, userB, ct);
            var mine = await CreateMediaFileAsync(userA, "foo.png", "image/png", ct);
            var notMine = await CreateMediaFileAsync(userB, "bar.png", "image/png", ct);

            await using var socketA = await ConnectAsync(userA, ct);

            (await SendExpectingErrorAsync(socketA, ChatMessage(conversationId, WithAttachments("mixed", mine, notMine), Guid.NewGuid()), ct))
                .Should().Be(InvalidAttachmentError);

            (await GetMessagesOldestFirstAsync(conversationId, ct)).Should().BeEmpty();
        }

        [Fact]
        public async Task EmptyMediaFileId_Rejected()
        {
            var ct = TestContext.Current.CancellationToken;
            var userA = await CreateUserAsync("userA1");
            var userB = await CreateUserAsync("userB1");
            var (_, conversationId) = await MakeFriendsAsync(userA, userB, ct);

            await using var socketA = await ConnectAsync(userA, ct);

            var request = new CreateChatMessageRequest
            {
                Content = "empty id",
                Attachments = new[] { new CreateChatAttachmentRequest { MediaFileId = Guid.Empty } }
            };
            await SendExpectingErrorAsync(socketA, ChatMessage(conversationId, request, Guid.NewGuid()), ct);

            (await GetMessagesOldestFirstAsync(conversationId, ct)).Should().BeEmpty();
        }

        [Fact]
        public async Task ContentTooLong_Rejected()
        {
            var ct = TestContext.Current.CancellationToken;
            var userA = await CreateUserAsync("userA1");
            var userB = await CreateUserAsync("userB1");
            var (_, conversationId) = await MakeFriendsAsync(userA, userB, ct);

            await using var socketA = await ConnectAsync(userA, ct);

            await SendExpectingErrorAsync(socketA, ChatMessage(conversationId, new string('a', 6001), Guid.NewGuid()), ct);
            (await GetMessagesOldestFirstAsync(conversationId, ct)).Should().BeEmpty();

            // The limit itself is valid
            var saved = await SendChatMessageAsync(socketA, conversationId, new string('a', 6000), ct);
            saved.Content.Should().HaveLength(6000);
        }

        private async Task<MediaFile> CreateMediaFileAsync(TestUser owner, string fileName, string contentType, CancellationToken ct)
        {
            var now = DateTime.UtcNow;
            var mediaFile = new MediaFile
            {
                Id = Guid.NewGuid(),
                FileName = fileName,
                Checksum = Guid.NewGuid().ToString("N"),
                ContentType = contentType,
                BytesSize = 1024,
                Url = $"http://localhost/media/{fileName}",
                OwnerId = owner.Id,
                CreatedAt = now,
                ModifiedAt = now
            };
            await mediaFileSaver.SaveAsync(mediaFile, ct);
            return mediaFile;
        }

        private static CreateChatMessageRequest WithAttachments(string content, params MediaFile[] files) => new()
        {
            Content = content,
            Attachments = files.Select(x => new CreateChatAttachmentRequest { MediaFileId = x.Id }).ToList()
        };

        private static void AssertAttachments(ChatMessageDto message, Guid messageId, params MediaFile[] files)
        {
            message.Attachments.Should().HaveCount(files.Length);
            message.Attachments.Select(x => x.MediaFileId).Should().BeEquivalentTo(files.Select(x => x.Id));

            foreach (var file in files)
            {
                var attachment = message.Attachments.First(x => x.MediaFileId == file.Id);
                attachment.Id.Should().NotBeEmpty();
                attachment.MessageId.Should().Be(messageId);
                attachment.FileName.Should().Be(file.FileName);
                attachment.ContentType.Should().Be(file.ContentType);
                attachment.Url.Should().NotBeNullOrEmpty();
                attachment.DownloadUrl.Should().NotBeNullOrEmpty();
            }
        }
    }
}
