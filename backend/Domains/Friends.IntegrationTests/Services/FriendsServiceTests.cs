using Autofac;
using FluentAssertions;
using HootOut.CommonDomain.DefaultValues;
using HootOut.CommonDomain.Persistence;
using HootOut.CommonIntegrationTests.PostgreSQL;
using HootOut.CommonIntegrationTests.S3;
using HootOut.Contracts.ChatConversations.Services;
using HootOut.Contracts.ChatMessage.Requests;
using HootOut.Contracts.ChatMessage.Services;
using HootOut.Contracts.Common.Enums;
using HootOut.Contracts.Friends.Requests;
using HootOut.Contracts.Friends.Services;
using HootOut.Contracts.Users.Dtos;
using HootOut.Contracts.Users.Dtos.Request;
using HootOut.Contracts.Users.Services;
using System;
using System.Collections.Generic;
using System.Text;

namespace HootOut.Friends.IntegrationTests.Services
{
    [Collection("IntegrationTests")]
    public class FriendsServiceTests : IClassFixture<PostgresTestContainer>, IClassFixture<TestS3Container>, IAsyncLifetime
    {
        private readonly IContainer container;
        private readonly ClearAllTables clearTables;
        private readonly IUserService userService;
        private readonly IFriendsService friendsService;
        private readonly IChatConversationsService chatConversationsService;
        private readonly IChatMessageService messageService;

        public FriendsServiceTests(PostgresTestContainer postgresContainer)
        {
            var builder = postgresContainer.RegisterTestPersistenceProvider();
            container = builder.Build();

            var testPersistenceProvider = container.Resolve<IPersistenceProvider>();
            Assert.Same(postgresContainer.postgreSQLProvider, testPersistenceProvider);

            clearTables = container.Resolve<ClearAllTables>();
            var defaultValues = container.Resolve<IEnumerable<IDefaultValues>>().OrderBy(x => x.Priority); ;

            foreach (var value in defaultValues)
            {
                value.InitAsync().Wait();
            }

            userService = container.Resolve<IUserService>();
            friendsService = container.Resolve<IFriendsService>();
            chatConversationsService = container.Resolve<IChatConversationsService>();
            messageService = container.Resolve<IChatMessageService>();
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
        private async Task SendFriendRequest_OK()
        {
            var ct = TestContext.Current.CancellationToken;
            var username1 = "Username 1";
            Guid userId = await CreateUserAsync(username1, ct);

            var username2 = "Username 2";
            Guid userId2 = await CreateUserAsync(username2, ct);

            var friendRequest = await friendsService.SendFriendRequestAsync(new SendFriendRequest
            {
                Username = username2
            }, userId, ct);

            friendRequest.Should().NotBeNull();
            friendRequest.Id.Should().NotBeEmpty();
            friendRequest.InvitedById.Should().Be(userId);
            friendRequest.UserId.Should().Be(userId2);
            friendRequest.Status.Should().Be(InvitationStatus.Pending);
        }

        [Fact]
        private async Task AcceptFriendRequest_OK()
        {
            var ct = TestContext.Current.CancellationToken;
            var username1 = "Username 1";
            Guid userId = await CreateUserAsync(username1, ct);

            var username2 = "Username 2";
            Guid userId2 = await CreateUserAsync(username2, ct);

            var friendRequest = await friendsService.SendFriendRequestAsync(new SendFriendRequest
            {
                Username = username2
            }, userId, ct);

            friendRequest.Should().NotBeNull();

            var relationId = await friendsService.UpdateFriendRequestAsync(new UpdateFriendRequest{ 
                Id = friendRequest.Id,
                Status = InvitationStatus.Accepted
            }, userId2, ct);

            relationId.Should().NotBeNull().And.NotBeEmpty();

            // Friends of user 1
            var friends1 = await friendsService.GetUserFriendsAsync(new Contracts.Common.PageRequest { }, userId, ct);
            friends1.Should().NotBeNull();
            friends1.Items.Should().NotBeNullOrEmpty();
            friends1.Items.Should().HaveCount(1);
            var first = friends1.Items.FirstOrDefault();
            first.Should().NotBeNull();
            first.Friend.Should().NotBeNull();
            first.Friend.Id.Should().Be(userId2);
            first.ChatConversationId.Should().NotBeEmpty();

            await friendsService.ValidateUserCanSendMessageAsync(first.ChatConversationId, userId, ct);

            // Friends of user 2
            var friends2 = await friendsService.GetUserFriendsAsync(new Contracts.Common.PageRequest { }, userId2, ct);
            friends2.Should().NotBeNull();
            friends2.Items.Should().NotBeNullOrEmpty();
            friends2.Items.Should().HaveCount(1);
            first = friends2.Items.FirstOrDefault();
            first.Should().NotBeNull();
            first.Friend.Should().NotBeNull();
            first.Friend.Id.Should().Be(userId);
            first.ChatConversationId.Should().NotBeEmpty();

            await friendsService.ValidateUserCanSendMessageAsync(first.ChatConversationId, userId2, ct);

            // Send direct messages

            var message = await messageService.CreateChatMessageAsync(new CreateChatMessageRequest
            {
                AuthorId = userId,
                ChatConversationId = first.ChatConversationId,
                Content = "Hello"
            }, ct);

            message.Should().NotBeNull();
            message.ChatConversationId.Should().Be(first.ChatConversationId);

            var message2 = await messageService.CreateChatMessageAsync(new CreateChatMessageRequest
            {
                AuthorId = userId2,
                ChatConversationId = first.ChatConversationId,
                Content = "Hello 2"
            }, ct);

            message2.Should().NotBeNull();
            message2.ChatConversationId.Should().Be(first.ChatConversationId);

            var messages = await messageService.GetMessagesByConversationIdAsync(first.ChatConversationId, ct: ct);

            messages.Should().NotBeNull();
            messages.Items.Should().NotBeNullOrEmpty();
            messages.Items.Should().HaveCount(2);
        }



        private async Task<UserDto> CreateUserDtoAsync(string? username, CancellationToken ct)
        {
            var random = Guid.NewGuid().ToString();
            return (await userService.CreateUserAsync(new CreateUserRequest
            {
                Email = random,
                Username = username ?? random,
                Password = random
            }));
        }

        private async Task<Guid> CreateUserAsync(string? username , CancellationToken ct)
        {
            return (await CreateUserDtoAsync(username, ct)).Id;
        }
    }
}
