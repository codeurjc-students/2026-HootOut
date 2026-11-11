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
using HootOut.Contracts.ChatMessage.Dtos;
using HootOut.Contracts.Common.Saver;
using HootOut.Infraestructure.DI;
using HootOut.MediaFiles.Entities;
using HootOut.Users.Entities;

namespace HootOut.PostgreSQL.IntegrationTests.Repositories.ChatMessages
{
    [Collection("IntegrationTests")]
    public class ChatMessageRepositoryTests : IClassFixture<PostgresTestContainer>, IClassFixture<TestS3Container>, IAsyncLifetime
    {
        private const int MaxPageSize = 50;

        private static readonly DateTime BaseDate = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        private readonly IContainer container;

        private readonly ClearAllTables clearTables;

        private readonly IChatMessageRepository repository;

        private readonly ISaver<UserInfo> userSaver;

        private readonly ISaver<ChatServer> serverSaver;

        private readonly ISaver<ChatChannel> channelSaver;

        private readonly ISaver<ChatMessage> messageSaver;

        private readonly ISaver<MediaFile> mediaFileSaver;

        private readonly ISaver<ChatMessageAttachment> attachmentSaver;

        private readonly IPersistenceProvider provider;

        public ChatMessageRepositoryTests(PostgresTestContainer postgresContainer)
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

            repository = container.Resolve<IChatMessageRepository>();
            userSaver = container.Resolve<ISaver<UserInfo>>();
            serverSaver = container.Resolve<ISaver<ChatServer>>();
            channelSaver = container.Resolve<ISaver<ChatChannel>>();
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

        #region Filters and mapping

        [Fact]
        public async Task SearchMessages_EmptyChannel_ReturnsEmptyPageWithoutNext()
        {
            var ct = TestContext.Current.CancellationToken;
            var (channel, _) = await CreateChannelAsync(ct);

            var result = await repository.SearchMessagesByChannelIdAsync(channel.Id, ct: ct);

            result.Items.Should().BeEmpty();
            result.Next.Should().BeNull();
            result.PageSize.Should().Be(10);
        }

        [Fact]
        public async Task SearchMessages_NotExistingChannel_ReturnsEmpty()
        {
            var ct = TestContext.Current.CancellationToken;
            var (channel, author) = await CreateChannelAsync(ct);
            await CreateMessageAsync(channel.Id, author.Id, BaseDate, ct);

            var result = await repository.SearchMessagesByChannelIdAsync(Guid.NewGuid(), ct: ct);

            result.Items.Should().BeEmpty();
            result.Next.Should().BeNull();
        }

        [Fact]
        public async Task SearchMessages_Existing_ReturnsMappedMessageAndAuthor()
        {
            var ct = TestContext.Current.CancellationToken;
            var (channel, author) = await CreateChannelAsync(ct);
            var message = await CreateMessageAsync(channel.Id, author.Id, BaseDate, ct, "hello world");

            var result = await repository.SearchMessagesByChannelIdAsync(channel.Id, ct: ct);

            result.Items.Should().ContainSingle();
            var dto = result.Items[0];
            dto.Id.Should().Be(message.Id);
            dto.ChatChannelId.Should().Be(channel.Id);
            dto.Content.Should().Be("hello world");
            // timestamptz stores microseconds while DateTime has 100 ns ticks
            dto.CreatedAt.Should().BeCloseTo(message.CreatedAt, TimeSpan.FromMilliseconds(1));
            dto.ModifiedAt.Should().BeCloseTo(message.ModifiedAt, TimeSpan.FromMilliseconds(1));
            dto.Author.Should().NotBeNull();
            dto.Author!.Id.Should().Be(author.Id);
            dto.Author.Username.Should().Be(author.Username);
            dto.Author.Email.Should().Be(author.Email);
            dto.Attachments.Should().BeEmpty();
        }

        [Fact]
        public async Task SearchMessages_ManyChannels_ReturnsOnlyMessagesOfRequestedChannel()
        {
            var ct = TestContext.Current.CancellationToken;
            var (channel, author) = await CreateChannelAsync(ct);
            var (otherChannel, otherAuthor) = await CreateChannelAsync(ct);
            var first = await CreateMessageAsync(channel.Id, author.Id, BaseDate, ct);
            var second = await CreateMessageAsync(channel.Id, author.Id, BaseDate.AddMinutes(1), ct);
            await CreateMessageAsync(otherChannel.Id, otherAuthor.Id, BaseDate.AddSeconds(30), ct);

            var result = await repository.SearchMessagesByChannelIdAsync(channel.Id, ct: ct);

            result.Items.Select(m => m.Id).Should().Equal(second.Id, first.Id);
            result.Items.Should().OnlyContain(m => m.ChatChannelId == channel.Id);
        }

        [Fact]
        public async Task SearchMessages_SeveralAuthors_MapsEachAuthor()
        {
            var ct = TestContext.Current.CancellationToken;
            var (channel, author) = await CreateChannelAsync(ct);
            var otherAuthor = await CreateUserAsync("other", ct);
            var mine = await CreateMessageAsync(channel.Id, author.Id, BaseDate, ct);
            var theirs = await CreateMessageAsync(channel.Id, otherAuthor.Id, BaseDate.AddMinutes(1), ct);

            var result = await repository.SearchMessagesByChannelIdAsync(channel.Id, ct: ct);

            result.Items.Single(m => m.Id == mine.Id).Author!.Id.Should().Be(author.Id);
            result.Items.Single(m => m.Id == theirs.Id).Author!.Id.Should().Be(otherAuthor.Id);
        }

        [Fact]
        public async Task SearchMessages_DeletedMessage_IsExcluded()
        {
            var ct = TestContext.Current.CancellationToken;
            var (channel, author) = await CreateChannelAsync(ct);
            var kept = await CreateMessageAsync(channel.Id, author.Id, BaseDate, ct);
            var deleted = await CreateMessageAsync(channel.Id, author.Id, BaseDate.AddMinutes(1), ct);
            await MarkAsDeletedAsync("ChatMessage", deleted.Id, ct);

            var result = await repository.SearchMessagesByChannelIdAsync(channel.Id, ct: ct);

            result.Items.Select(m => m.Id).Should().Equal(kept.Id);
        }

        #endregion

        #region Order

        [Fact]
        public async Task SearchMessages_DifferentCreatedAt_OrderedByCreatedAtDescending()
        {
            var ct = TestContext.Current.CancellationToken;
            var (channel, author) = await CreateChannelAsync(ct);
            // Inserted out of order so the result does not depend on insertion order
            var second = await CreateMessageAsync(channel.Id, author.Id, BaseDate.AddMinutes(1), ct);
            var third = await CreateMessageAsync(channel.Id, author.Id, BaseDate.AddMinutes(2), ct);
            var first = await CreateMessageAsync(channel.Id, author.Id, BaseDate, ct);

            var result = await repository.SearchMessagesByChannelIdAsync(channel.Id, ct: ct);

            result.Items.Select(m => m.Id).Should().Equal(third.Id, second.Id, first.Id);
        }

        [Fact]
        public async Task SearchMessages_SameCreatedAt_OrderedByIdDescending()
        {
            var ct = TestContext.Current.CancellationToken;
            var (channel, author) = await CreateChannelAsync(ct);
            var messages = new List<ChatMessage>();
            for (var i = 0; i < 5; i++)
            {
                messages.Add(await CreateMessageAsync(channel.Id, author.Id, BaseDate, ct));
            }

            var result = await repository.SearchMessagesByChannelIdAsync(channel.Id, ct: ct);

            // PostgreSQL compares uuids byte by byte, which matches the order of their lowercase string form
            var expected = messages.Select(m => m.Id).OrderByDescending(id => id.ToString(), StringComparer.Ordinal);
            result.Items.Select(m => m.Id).Should().Equal(expected);
        }

        #endregion

        #region Attachments

        [Fact]
        public async Task SearchMessages_MessageWithAttachments_ReturnsMappedAttachments()
        {
            var ct = TestContext.Current.CancellationToken;
            var (channel, author) = await CreateChannelAsync(ct);
            var message = await CreateMessageAsync(channel.Id, author.Id, BaseDate, ct);
            var mediaFile = await CreateMediaFileAsync("report.pdf", "application/pdf", ct);
            var attachment = await AddAttachmentAsync(message.Id, mediaFile.Id, BaseDate, ct);

            var result = await repository.SearchMessagesByChannelIdAsync(channel.Id, ct: ct);

            var dto = result.Items.Should().ContainSingle().Subject.Attachments.Should().ContainSingle().Subject;
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
        public async Task SearchMessages_ManyAttachments_OrderedByCreatedAtThenId()
        {
            var ct = TestContext.Current.CancellationToken;
            var (channel, author) = await CreateChannelAsync(ct);
            var message = await CreateMessageAsync(channel.Id, author.Id, BaseDate, ct);
            var last = await AddAttachmentAsync(message.Id, (await CreateMediaFileAsync("last.png", "image/png", ct)).Id, BaseDate.AddMinutes(1), ct);
            var tied = new List<ChatMessageAttachment>();
            for (var i = 0; i < 3; i++)
            {
                tied.Add(await AddAttachmentAsync(message.Id, (await CreateMediaFileAsync($"{i}.png", "image/png", ct)).Id, BaseDate, ct));
            }

            var result = await repository.SearchMessagesByChannelIdAsync(channel.Id, ct: ct);

            var expected = tied.Select(a => a.Id).OrderBy(id => id.ToString(), StringComparer.Ordinal).Append(last.Id);
            result.Items.Single().Attachments.Select(a => a.Id).Should().Equal(expected);
        }

        [Fact]
        public async Task SearchMessages_SeveralMessagesWithAttachments_AssignsEachAttachmentToItsMessage()
        {
            var ct = TestContext.Current.CancellationToken;
            var (channel, author) = await CreateChannelAsync(ct);
            var first = await CreateMessageAsync(channel.Id, author.Id, BaseDate, ct);
            var second = await CreateMessageAsync(channel.Id, author.Id, BaseDate.AddMinutes(1), ct);
            var withoutAttachments = await CreateMessageAsync(channel.Id, author.Id, BaseDate.AddMinutes(2), ct);
            var firstAttachments = new[]
            {
                await AddAttachmentAsync(first.Id, (await CreateMediaFileAsync("1a.png", "image/png", ct)).Id, BaseDate, ct),
                await AddAttachmentAsync(first.Id, (await CreateMediaFileAsync("1b.png", "image/png", ct)).Id, BaseDate.AddSeconds(1), ct)
            };
            var secondAttachment = await AddAttachmentAsync(second.Id, (await CreateMediaFileAsync("2.png", "image/png", ct)).Id, BaseDate, ct);

            var result = await repository.SearchMessagesByChannelIdAsync(channel.Id, ct: ct);

            result.Items.Select(m => m.Id).Should().Equal(withoutAttachments.Id, second.Id, first.Id);
            result.Items[0].Attachments.Should().BeEmpty();
            result.Items[1].Attachments.Select(a => a.Id).Should().Equal(secondAttachment.Id);
            result.Items[2].Attachments.Select(a => a.Id).Should().Equal(firstAttachments.Select(a => a.Id));
            result.Items.Should().OnlyContain(m => m.Attachments.All(a => a.MessageId == m.Id));
        }

        [Fact]
        public async Task SearchMessages_DeletedAttachment_IsExcluded()
        {
            var ct = TestContext.Current.CancellationToken;
            var (channel, author) = await CreateChannelAsync(ct);
            var message = await CreateMessageAsync(channel.Id, author.Id, BaseDate, ct);
            var kept = await AddAttachmentAsync(message.Id, (await CreateMediaFileAsync("kept.png", "image/png", ct)).Id, BaseDate, ct);
            var deleted = await AddAttachmentAsync(message.Id, (await CreateMediaFileAsync("deleted.png", "image/png", ct)).Id, BaseDate.AddSeconds(1), ct);
            await MarkAsDeletedAsync("ChatMessageAttachment", deleted.Id, ct);

            var result = await repository.SearchMessagesByChannelIdAsync(channel.Id, ct: ct);

            result.Items.Single().Attachments.Select(a => a.Id).Should().Equal(kept.Id);
        }

        [Fact]
        public async Task SearchMessages_DeletedMediaFile_AttachmentIsExcluded()
        {
            var ct = TestContext.Current.CancellationToken;
            var (channel, author) = await CreateChannelAsync(ct);
            var message = await CreateMessageAsync(channel.Id, author.Id, BaseDate, ct);
            var kept = await AddAttachmentAsync(message.Id, (await CreateMediaFileAsync("kept.png", "image/png", ct)).Id, BaseDate, ct);
            var deletedFile = await CreateMediaFileAsync("deleted.png", "image/png", ct);
            await AddAttachmentAsync(message.Id, deletedFile.Id, BaseDate.AddSeconds(1), ct);
            await MarkAsDeletedAsync("MediaFile", deletedFile.Id, ct);

            var result = await repository.SearchMessagesByChannelIdAsync(channel.Id, ct: ct);

            result.Items.Single().Attachments.Select(a => a.Id).Should().Equal(kept.Id);
        }

        [Fact]
        public async Task SearchMessages_AllAttachmentsDeleted_ReturnsMessageWithoutAttachments()
        {
            var ct = TestContext.Current.CancellationToken;
            var (channel, author) = await CreateChannelAsync(ct);
            var message = await CreateMessageAsync(channel.Id, author.Id, BaseDate, ct);
            var deletedAttachment = await AddAttachmentAsync(message.Id, (await CreateMediaFileAsync("a.png", "image/png", ct)).Id, BaseDate, ct);
            var deletedFile = await CreateMediaFileAsync("b.png", "image/png", ct);
            await AddAttachmentAsync(message.Id, deletedFile.Id, BaseDate.AddSeconds(1), ct);
            await MarkAsDeletedAsync("ChatMessageAttachment", deletedAttachment.Id, ct);
            await MarkAsDeletedAsync("MediaFile", deletedFile.Id, ct);

            var result = await repository.SearchMessagesByChannelIdAsync(channel.Id, ct: ct);

            // The message must still be returned, with no null placeholder attachments
            var dto = result.Items.Should().ContainSingle().Subject;
            dto.Id.Should().Be(message.Id);
            dto.Attachments.Should().BeEmpty();
        }

        [Fact]
        public async Task SearchMessages_MessagesWithManyAttachments_CountOnceForPageSize()
        {
            var ct = TestContext.Current.CancellationToken;
            var (channel, author) = await CreateChannelAsync(ct);
            var messages = new List<ChatMessage>();
            for (var i = 0; i < 3; i++)
            {
                var message = await CreateMessageAsync(channel.Id, author.Id, BaseDate.AddMinutes(i), ct);
                for (var j = 0; j < 3; j++)
                {
                    await AddAttachmentAsync(message.Id, (await CreateMediaFileAsync($"{i}-{j}.png", "image/png", ct)).Id, BaseDate.AddSeconds(j), ct);
                }
                messages.Add(message);
            }

            var result = await repository.SearchMessagesByChannelIdAsync(channel.Id, pageSize: 2, ct: ct);

            // LIMIT is applied to messages in the CTE, before joining the attachments
            result.Items.Select(m => m.Id).Should().Equal(messages[2].Id, messages[1].Id);
            result.Items.Should().OnlyContain(m => m.Attachments.Count == 3);
            result.Next.Should().Be(messages[1].Id);
        }

        #endregion

        #region Page size

        [Fact]
        public async Task SearchMessages_DefaultPageSize_ReturnsTenItems()
        {
            var ct = TestContext.Current.CancellationToken;
            var (channel, author) = await CreateChannelAsync(ct);
            await CreateMessagesAsync(channel.Id, author.Id, 11, ct);

            var result = await repository.SearchMessagesByChannelIdAsync(channel.Id, ct: ct);

            result.PageSize.Should().Be(10);
            result.Items.Should().HaveCount(10);
            result.Next.Should().NotBeNull();
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        [InlineData(int.MinValue)]
        public async Task SearchMessages_PageSizeBelowOne_IsClampedToOne(int pageSize)
        {
            var ct = TestContext.Current.CancellationToken;
            var (channel, author) = await CreateChannelAsync(ct);
            var messages = await CreateMessagesAsync(channel.Id, author.Id, 3, ct);

            var result = await repository.SearchMessagesByChannelIdAsync(channel.Id, pageSize: pageSize, ct: ct);

            result.PageSize.Should().Be(1);
            result.Items.Select(m => m.Id).Should().Equal(messages[^1].Id);
            result.Next.Should().Be(messages[^1].Id);
        }

        [Theory]
        [InlineData(MaxPageSize + 1)]
        [InlineData(1000)]
        [InlineData(int.MaxValue)]
        public async Task SearchMessages_PageSizeAboveMax_IsClampedToMax(int pageSize)
        {
            var ct = TestContext.Current.CancellationToken;
            var (channel, author) = await CreateChannelAsync(ct);
            await CreateMessagesAsync(channel.Id, author.Id, MaxPageSize + 2, ct);

            var result = await repository.SearchMessagesByChannelIdAsync(channel.Id, pageSize: pageSize, ct: ct);

            result.PageSize.Should().Be(MaxPageSize);
            result.Items.Should().HaveCount(MaxPageSize);
            result.Next.Should().NotBeNull();
        }

        #endregion

        #region Pagination

        [Fact]
        public async Task SearchMessages_FirstPage_HasNextPointingToLastItem()
        {
            var ct = TestContext.Current.CancellationToken;
            var (channel, author) = await CreateChannelAsync(ct);
            var messages = await CreateMessagesAsync(channel.Id, author.Id, 5, ct);

            var result = await repository.SearchMessagesByChannelIdAsync(channel.Id, pageSize: 2, ct: ct);

            result.Items.Select(m => m.Id).Should().Equal(messages[4].Id, messages[3].Id);
            result.Next.Should().Be(messages[3].Id);
            result.PageSize.Should().Be(2);
        }

        [Fact]
        public async Task SearchMessages_ExactFit_HasNoNext()
        {
            var ct = TestContext.Current.CancellationToken;
            var (channel, author) = await CreateChannelAsync(ct);
            await CreateMessagesAsync(channel.Id, author.Id, 3, ct);

            var result = await repository.SearchMessagesByChannelIdAsync(channel.Id, pageSize: 3, ct: ct);

            result.Items.Should().HaveCount(3);
            result.Next.Should().BeNull();
        }

        [Fact]
        public async Task SearchMessages_LastPartialPage_HasNoNext()
        {
            var ct = TestContext.Current.CancellationToken;
            var (channel, author) = await CreateChannelAsync(ct);
            var messages = await CreateMessagesAsync(channel.Id, author.Id, 5, ct);

            var firstPage = await repository.SearchMessagesByChannelIdAsync(channel.Id, pageSize: 3, ct: ct);
            var secondPage = await repository.SearchMessagesByChannelIdAsync(channel.Id, firstPage.Next, 3, ct);

            secondPage.Items.Select(m => m.Id).Should().Equal(messages[1].Id, messages[0].Id);
            secondPage.Next.Should().BeNull();
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(7)]
        public async Task SearchMessages_WalkAllPages_MatchesSingleQuery(int pageSize)
        {
            var ct = TestContext.Current.CancellationToken;
            var (channel, author) = await CreateChannelAsync(ct);
            // Two messages per minute, so there are CreatedAt ties across page boundaries
            for (var i = 0; i < 10; i++)
            {
                await CreateMessageAsync(channel.Id, author.Id, BaseDate.AddMinutes(i / 2), ct);
            }

            var expected = await repository.SearchMessagesByChannelIdAsync(channel.Id, pageSize: MaxPageSize, ct: ct);
            var walked = await ReadAllPagesAsync(channel.Id, pageSize, ct);

            expected.Items.Should().HaveCount(10);
            walked.Select(m => m.Id).Should().Equal(expected.Items.Select(m => m.Id));
        }

        [Fact]
        public async Task SearchMessages_WalkAllPagesWithAttachments_KeepsAttachmentsWithTheirMessage()
        {
            var ct = TestContext.Current.CancellationToken;
            var (channel, author) = await CreateChannelAsync(ct);
            var expectedAttachments = new Dictionary<Guid, List<Guid>>();
            for (var i = 0; i < 5; i++)
            {
                var message = await CreateMessageAsync(channel.Id, author.Id, BaseDate.AddMinutes(i), ct);
                expectedAttachments[message.Id] = [];
                for (var j = 0; j < i % 3; j++)
                {
                    var attachment = await AddAttachmentAsync(message.Id, (await CreateMediaFileAsync($"{i}-{j}.png", "image/png", ct)).Id, BaseDate.AddSeconds(j), ct);
                    expectedAttachments[message.Id].Add(attachment.Id);
                }
            }

            var walked = await ReadAllPagesAsync(channel.Id, 2, ct);

            walked.Should().HaveCount(5);
            foreach (var message in walked)
            {
                message.Attachments.Select(a => a.Id).Should().Equal(expectedAttachments[message.Id]);
            }
        }

        [Fact]
        public async Task SearchMessages_WalkAllPages_SkipsDeletedMessages()
        {
            var ct = TestContext.Current.CancellationToken;
            var (channel, author) = await CreateChannelAsync(ct);
            var messages = await CreateMessagesAsync(channel.Id, author.Id, 6, ct);
            await MarkAsDeletedAsync("ChatMessage", messages[1].Id, ct);
            await MarkAsDeletedAsync("ChatMessage", messages[4].Id, ct);

            var walked = await ReadAllPagesAsync(channel.Id, 2, ct);

            walked.Select(m => m.Id).Should().Equal(messages[5].Id, messages[3].Id, messages[2].Id, messages[0].Id);
        }

        [Fact]
        public async Task SearchMessages_CursorMessageDeletedBetweenPages_ContinuesFromItsPosition()
        {
            var ct = TestContext.Current.CancellationToken;
            var (channel, author) = await CreateChannelAsync(ct);
            var messages = await CreateMessagesAsync(channel.Id, author.Id, 5, ct);

            var firstPage = await repository.SearchMessagesByChannelIdAsync(channel.Id, pageSize: 2, ct: ct);
            // The cursor subquery does not filter by Deleted, so the deleted message still sets the position
            await MarkAsDeletedAsync("ChatMessage", firstPage.Next!.Value, ct);
            var secondPage = await repository.SearchMessagesByChannelIdAsync(channel.Id, firstPage.Next, 2, ct);

            secondPage.Items.Select(m => m.Id).Should().Equal(messages[2].Id, messages[1].Id);
        }

        [Fact]
        public async Task SearchMessages_NewMessageAfterFirstPage_DoesNotShiftNextPage()
        {
            var ct = TestContext.Current.CancellationToken;
            var (channel, author) = await CreateChannelAsync(ct);
            var messages = await CreateMessagesAsync(channel.Id, author.Id, 4, ct);

            var firstPage = await repository.SearchMessagesByChannelIdAsync(channel.Id, pageSize: 2, ct: ct);
            await CreateMessageAsync(channel.Id, author.Id, BaseDate.AddHours(1), ct);
            var secondPage = await repository.SearchMessagesByChannelIdAsync(channel.Id, firstPage.Next, 2, ct);

            secondPage.Items.Select(m => m.Id).Should().Equal(messages[1].Id, messages[0].Id);
            secondPage.Next.Should().BeNull();
        }

        [Fact]
        public async Task SearchMessages_NotExistingCursor_ReturnsEmpty()
        {
            var ct = TestContext.Current.CancellationToken;
            var (channel, author) = await CreateChannelAsync(ct);
            await CreateMessagesAsync(channel.Id, author.Id, 3, ct);

            var result = await repository.SearchMessagesByChannelIdAsync(channel.Id, Guid.NewGuid(), 10, ct);

            // The cursor subquery returns NULL and the row comparison against NULL matches nothing
            result.Items.Should().BeEmpty();
            result.Next.Should().BeNull();
        }

        [Fact]
        public async Task SearchMessages_CursorIsOldestMessage_ReturnsEmpty()
        {
            var ct = TestContext.Current.CancellationToken;
            var (channel, author) = await CreateChannelAsync(ct);
            var messages = await CreateMessagesAsync(channel.Id, author.Id, 3, ct);

            var result = await repository.SearchMessagesByChannelIdAsync(channel.Id, messages[0].Id, 10, ct);

            result.Items.Should().BeEmpty();
            result.Next.Should().BeNull();
        }

        #endregion

        [Fact]
        public async Task SearchMessages_CancelledToken_ThrowsOperationCanceled()
        {
            var (channel, _) = await CreateChannelAsync(TestContext.Current.CancellationToken);
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            var act = () => repository.SearchMessagesByChannelIdAsync(channel.Id, ct: cts.Token);

            await act.Should().ThrowAsync<OperationCanceledException>();
        }

        private async Task<List<ChatMessageDto>> ReadAllPagesAsync(Guid channelId, int pageSize, CancellationToken ct)
        {
            var all = new List<ChatMessageDto>();
            Guid? next = null;
            var guard = 0;
            do
            {
                var page = await repository.SearchMessagesByChannelIdAsync(channelId, next, pageSize, ct);
                all.AddRange(page.Items);
                next = page.Next;
                (++guard).Should().BeLessThan(100, "pagination must end");
            }
            while (next is not null);

            return all;
        }

        private async Task<UserInfo> CreateUserAsync(string username, CancellationToken ct)
        {
            var user = Persistable.CreateNew<UserInfo>();
            user.Username = $"{username}-{user.Id}";
            user.Email = $"{user.Id}@test.com";
            user.Password = $"hash-{username}";
            await userSaver.SaveAsync(user, ct);
            return user;
        }

        /// <summary>
        /// Creates a channel in a new server, owned by a new user that is returned as the default author.
        /// </summary>
        private async Task<(ChatChannel Channel, UserInfo Author)> CreateChannelAsync(CancellationToken ct)
        {
            var author = await CreateUserAsync("author", ct);

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

        private async Task<ChatMessage> CreateMessageAsync(Guid channelId, Guid authorId, DateTime createdAt, CancellationToken ct, string? content = null)
        {
            var message = new ChatMessage
            {
                Id = Guid.CreateVersion7(),
                ChatChannelId = channelId,
                AuthorId = authorId,
                Content = content ?? $"message at {createdAt:O}",
                CreatedAt = createdAt,
                ModifiedAt = createdAt
            };
            await messageSaver.SaveAsync(message, ct);
            return message;
        }

        /// <summary>
        /// Creates messages one minute apart; the returned list goes from the oldest to the newest.
        /// </summary>
        private async Task<List<ChatMessage>> CreateMessagesAsync(Guid channelId, Guid authorId, int count, CancellationToken ct)
        {
            var messages = new List<ChatMessage>();
            for (var i = 0; i < count; i++)
            {
                messages.Add(await CreateMessageAsync(channelId, authorId, BaseDate.AddMinutes(i), ct));
            }

            return messages;
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

        private async Task<ChatMessageAttachment> AddAttachmentAsync(Guid messageId, Guid mediaFileId, DateTime createdAt, CancellationToken ct)
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
