using Autofac;
using FluentAssertions;
using FluentValidation;
using HootOut.ChatServers.Repositories;
using HootOut.CommonDomain.DefaultValues;
using HootOut.CommonDomain.Persistence;
using HootOut.CommonIntegrationTests.PostgreSQL;
using HootOut.CommonIntegrationTests.S3;
using HootOut.Contracts.ChatServers.Dtos;
using HootOut.Contracts.ChatServers.Enums;
using HootOut.Contracts.ChatServers.Requests;
using HootOut.Contracts.ChatServers.Services;
using HootOut.Contracts.Common;
using HootOut.Contracts.Users.Dtos;
using HootOut.Contracts.Users.Dtos.Request;
using HootOut.Contracts.Users.Services;

namespace HootOut.ChatServers.IntegrationTests.Services
{
    [Collection("IntegrationTests")]
    public class ChatServerServiceTests : IClassFixture<PostgresTestContainer>, IClassFixture<TestS3Container>, IAsyncLifetime
    {
        private IContainer container;
        private ClearAllTables clearTables;

        private IChatServerService chatServerService;
        IServerMemberRepository memberRepository;
        IServerInvitationRepository invitationRepository;
        IChatServerRepository serverRepository;

        public ChatServerServiceTests(PostgresTestContainer postgresContainer)
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

            chatServerService = container.Resolve<IChatServerService>();
            memberRepository = container.Resolve<IServerMemberRepository>();
            invitationRepository = container.Resolve<IServerInvitationRepository>();
            serverRepository = container.Resolve<IChatServerRepository>();
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
        public async Task GetChatServersByUser_Empty()
        {
            var ct = TestContext.Current.CancellationToken;

            (await chatServerService.GetServersByUserAsync(Guid.NewGuid(), ct)).Should().BeEmpty();
        }

        [Fact]
        public async Task GetChatServersByUser_One()
        {
            var ct = TestContext.Current.CancellationToken;
            Guid userId = await CreateUserAsync(ct);
            string name = "nameTest";
            string description = "test";
            Guid serverId = Guid.NewGuid();
            var server = await chatServerService.CreateChatServerAsync(new CreateChatServerRequest
            {
                Name = name,
                Description = description
            }, userId, ct);

            server.Should().NotBeNull();
            server.Id.Should().NotBeEmpty();
            server.OwnerId.Should().Be(userId);
            server.Name.Should().Be(name);
            server.Description.Should().Be(description);

            var servers = await chatServerService.GetServersByUserAsync(userId, ct);

            servers.Should().NotBeNull();
            servers.Should().HaveCount(1);

            var first = servers.FirstOrDefault();
            first.Should().NotBeNull();
            first.Id.Should().Be(server.Id);
            first.OwnerId.Should().Be(server.OwnerId);
            first.Name.Should().Be(server.Name);
            first.Description.Should().Be(server.Description);

            var emptyServers = await chatServerService.GetServersByUserAsync(await CreateUserAsync(ct), ct);
            emptyServers.Should().NotBeNull();
            emptyServers.Should().BeEmpty();
        }

        [Fact]
        public async Task GetChatServersByUser_Many()
        {
            var ct = TestContext.Current.CancellationToken;
            Guid userId = await CreateUserAsync(ct);
            var server1 = await chatServerService.CreateChatServerAsync(new CreateChatServerRequest { Name = "test1" }, userId, ct);
            var server2 = await chatServerService.CreateChatServerAsync(new CreateChatServerRequest { Name = "test2" }, userId, ct);
            var server3 = await chatServerService.CreateChatServerAsync(new CreateChatServerRequest { Name = "test3" }, userId, ct);

            // Other servers
            var userId2 = await CreateUserAsync(ct);
            await chatServerService.CreateChatServerAsync(new CreateChatServerRequest { Name = "12345" }, userId2, ct);
            await chatServerService.CreateChatServerAsync(new CreateChatServerRequest { Name = "12345" }, userId2, ct);

            await chatServerService.CreateChatServerAsync(new CreateChatServerRequest { Name = "test555" }, await CreateUserAsync(ct), ct);
            await chatServerService.CreateChatServerAsync(new CreateChatServerRequest { Name = "Different names" }, await CreateUserAsync(ct), ct);

            var servers = await chatServerService.GetServersByUserAsync(userId, ct);

            servers.Should().NotBeNull();
            servers.Should().HaveCount(3);

            var first = servers.Where(x => x.Id == server1.Id).FirstOrDefault();
            first.Should().NotBeNull();
            first.OwnerId.Should().Be(server1.OwnerId);

            var servers2 = await chatServerService.GetServersByUserAsync(userId2, ct);
            servers2.Should().NotBeNull();
            servers2.Should().HaveCount(2);

            var emptyServers = await chatServerService.GetServersByUserAsync(await CreateUserAsync(ct), ct);
            emptyServers.Should().NotBeNull();
            emptyServers.Should().BeEmpty();
        }


        [Fact]
        public async Task InviteUserToAServer_OK()
        {
            var ct = TestContext.Current.CancellationToken;
            var username1 = "Username 1";
            Guid userId = await CreateUserAsync(ct, username1);

            var username2 = "Username 2";
            Guid userId2 = await CreateUserAsync(ct, username2);
            var server = await chatServerService.CreateChatServerAsync(new CreateChatServerRequest { Name = "test1" }, userId, ct);

            // Only Creator is the member
            var servers = await chatServerService.GetServersByUserAsync(userId, ct);
            servers.Should().NotBeNull();
            servers.Should().HaveCount(1);

            var servers2 = await chatServerService.GetServersByUserAsync(userId2, ct);
            servers2.Should().NotBeNull();
            servers2.Should().BeEmpty();

            // Invite the second user to the server. It is not a member yet

            await chatServerService.InviteMemberAsync(new InviteServerMemberRequest
            {
                ChatServerId = server.Id,
                Username = username2
            }, userId, ct);

        }

        [Fact]
        public async Task InviteUserToAServer_UserAccepts()
        {
            var ct = TestContext.Current.CancellationToken;

            var username1 = "Username 1";
            Guid userId = await CreateUserAsync(ct, username1);

            var username2 = "Username 2";
            Guid userId2 = await CreateUserAsync(ct, username2);
            var server = await chatServerService.CreateChatServerAsync(new CreateChatServerRequest { Name = "test1" }, userId, ct);

            var servers = await chatServerService.GetServersByUserAsync(userId2, ct);
            servers.Should().BeEmpty();

            var member = await memberRepository.GetServerMemberAsync(server.Id, userId, ct);
            member.Should().NotBeNull();

            member = await memberRepository.GetServerMemberAsync(server.Id, userId2, ct);
            member.Should().BeNull();

            // Invite User
            var invitation = await chatServerService.InviteMemberAsync(
                new InviteServerMemberRequest
                {
                    ChatServerId = server.Id,
                    Username = username2,
                }, userId, ct);
            invitation.Should().NotBeNull();
            invitation.Id.Should().NotBeEmpty();
            invitation.ChatServerId.Should().Be(server.Id);
            invitation.UserId.Should().Be(userId2);
            invitation.Status.Should().Be(ServerInvitationStatus.Pending);

            var dbInvitation = await invitationRepository.GetServerInvitationByIdAsync(invitation.Id, ct);
            dbInvitation.Should().NotBeNull();
            dbInvitation.Id.Should().Be(invitation.Id);
            dbInvitation.UserId.Should().Be(userId2);
            dbInvitation.InvitedBy.Should().Be(userId);
            dbInvitation.Status.Should().Be(ServerInvitationStatus.Pending);

            // User accepts invitation
            await chatServerService.UpdateServerInvitationAsync(
                new UpdateServerInvitationRequest
                {
                    Id = dbInvitation.Id,
                    Status = ServerInvitationStatus.Accepted
                }, userId2, ct);

            await ValidateServerInvitationAcceptedAsync(memberRepository, invitationRepository, invitation, userId, userId2, server.Id, ct);

            servers = await chatServerService.GetServersByUserAsync(userId2, ct);
            servers.Should().HaveCount(1);
            servers.First().Id.Should().Be(server.Id);
        }

        [Fact]
        public async Task InviteUserToAServer_MultipleInvitations()
        {
            var ct = TestContext.Current.CancellationToken;

            var username1 = "Username 1";
            Guid userId = await CreateUserAsync(ct, username1);

            var username2 = "Username 2";
            Guid userId2 = await CreateUserAsync(ct, username2);
            var server = await chatServerService.CreateChatServerAsync(new CreateChatServerRequest { Name = "test1" }, userId, ct);

            // Invite member
            var invitation = await chatServerService.InviteMemberAsync(
                new InviteServerMemberRequest
                {
                    ChatServerId = server.Id,
                    Username = username2,
                }, userId, ct);

            // Invite again the same member
            await Assert.ThrowsAnyAsync<Exception>(async () => await chatServerService.InviteMemberAsync(
                new InviteServerMemberRequest
                {
                    ChatServerId = server.Id,
                    Username = username2,
                }, userId, ct));

            // Accepts Invitation
            await chatServerService.UpdateServerInvitationAsync(
                new UpdateServerInvitationRequest
                {
                    Id = invitation.Id,
                    Status = ServerInvitationStatus.Accepted
                }, userId2, ct);

            // Validates Invitation is accepted
            await ValidateServerInvitationAcceptedAsync(memberRepository, invitationRepository, invitation, userId, userId2, server.Id, ct);
        }

        [Fact]
        public async Task InviteUserToAServer_AlreadyAMember()
        {
            var ct = TestContext.Current.CancellationToken;


            var username1 = "Username 1";
            Guid userId = await CreateUserAsync(ct, username1);

            var username2 = "Username 2";
            Guid userId2 = await CreateUserAsync(ct, username2);
            var server = await chatServerService.CreateChatServerAsync(new CreateChatServerRequest { Name = "test1" }, userId, ct);

            // Invite member
            var invitation = await chatServerService.InviteMemberAsync(
                new InviteServerMemberRequest
                {
                    ChatServerId = server.Id,
                    Username = username2,
                }, userId, ct);

            // Accepts Invitation
            await chatServerService.UpdateServerInvitationAsync(
                new UpdateServerInvitationRequest
                {
                    Id = invitation.Id,
                    Status = ServerInvitationStatus.Accepted
                }, userId2, ct);

            // Invites Again
            await Assert.ThrowsAsync<ValidationException>(async () => await chatServerService.InviteMemberAsync(
                new InviteServerMemberRequest
                {
                    ChatServerId = server.Id,
                    Username = username2,
                }, userId, ct));

            // Validates Invitation is accepted
            await ValidateServerInvitationAcceptedAsync(memberRepository, invitationRepository, invitation, userId, userId2, server.Id, ct);
        }

        [Fact]
        public async Task InviteUserToAServer_MemberTriesToAcceptAnotherInvitation()
        {
            var ct = TestContext.Current.CancellationToken;


            var username1 = "Username 1";
            Guid userId = await CreateUserAsync(ct, username1);

            var username2 = "Username 2";
            Guid userId2 = await CreateUserAsync(ct, username2);
            var server = await chatServerService.CreateChatServerAsync(new CreateChatServerRequest { Name = "test1" }, userId, ct);

            // Invite member
            var invitation = await chatServerService.InviteMemberAsync(
                new InviteServerMemberRequest
                {
                    ChatServerId = server.Id,
                    Username = username2,
                }, userId, ct);

            // Accepts other user Invitation
            await Assert.ThrowsAsync<ValidationException>(async () => await chatServerService.UpdateServerInvitationAsync(
                new UpdateServerInvitationRequest
                {
                    Id = invitation.Id,
                    Status = ServerInvitationStatus.Accepted
                }, userId, ct));

            await ValidateServerInvitationPendingAsync(memberRepository, invitationRepository, invitation, userId, userId2, server.Id, ct);

            // User 2 Accepts Invitation
            await chatServerService.UpdateServerInvitationAsync(
                new UpdateServerInvitationRequest
                {
                    Id = invitation.Id,
                    Status = ServerInvitationStatus.Accepted
                }, userId2, ct);

            // Validates Invitation is accepted
            await ValidateServerInvitationAcceptedAsync(memberRepository, invitationRepository, invitation, userId, userId2, server.Id, ct);
        }

        [Fact]

        public async Task InviteUserToAServer_User3TriesToAcceptAnotherInvitation()
        {
            var ct = TestContext.Current.CancellationToken;


            var username1 = "Username 1";
            Guid userId = await CreateUserAsync(ct, username1);

            var username2 = "Username 2";
            Guid userId2 = await CreateUserAsync(ct, username2);

            var username3 = "Username 3";
            Guid userId3 = await CreateUserAsync(ct, username3);

            var server = await chatServerService.CreateChatServerAsync(new CreateChatServerRequest { Name = "test1" }, userId, ct);

            // Invite user 2
            var invitation2 = await chatServerService.InviteMemberAsync(
                new InviteServerMemberRequest
                {
                    ChatServerId = server.Id,
                    Username = username2,
                }, userId, ct);

            // Invite user 3

            var invitation3 = await chatServerService.InviteMemberAsync(
                new InviteServerMemberRequest
                {
                    ChatServerId = server.Id,
                    Username = username3,
                }, userId, ct);

            // User 3 tries to accept user 2 invitation 2
            await Assert.ThrowsAsync<ValidationException>(async () => await chatServerService.UpdateServerInvitationAsync(
                new UpdateServerInvitationRequest
                {
                    Id = invitation2.Id,
                    Status = ServerInvitationStatus.Accepted
                }, userId3, ct));

            // Validate user2 invitation is still pending
            await ValidateServerInvitationPendingAsync(memberRepository, invitationRepository, invitation2, userId, userId2, server.Id, ct);

            // Validate user3 invitation is still pending
            await ValidateServerInvitationPendingAsync(memberRepository, invitationRepository, invitation3, userId, userId3, server.Id, ct);

            // User 2 Accepts Invitation 2
            await chatServerService.UpdateServerInvitationAsync(
                new UpdateServerInvitationRequest
                {
                    Id = invitation2.Id,
                    Status = ServerInvitationStatus.Accepted
                }, userId2, ct);

            // Validates Invitation 2 is accepted
            await ValidateServerInvitationAcceptedAsync(memberRepository, invitationRepository, invitation2, userId, userId2, server.Id, ct);

            // User 3 Accepts Invitation 3
            await chatServerService.UpdateServerInvitationAsync(
                new UpdateServerInvitationRequest
                {
                    Id = invitation3.Id,
                    Status = ServerInvitationStatus.Accepted
                }, userId3, ct);

            // Validates Invitation 3 is accepted
            await ValidateServerInvitationAcceptedAsync(memberRepository, invitationRepository, invitation3, userId, userId3, server.Id, ct);
        }

        [Fact]
        public async Task InviteUserToAServer_UserBecomesMemberAndCanInvite()
        {
            var ct = TestContext.Current.CancellationToken;


            var username1 = "Username 1";
            Guid userId = await CreateUserAsync(ct, username1);

            var username2 = "Username 2";
            Guid userId2 = await CreateUserAsync(ct, username2);

            var username3 = "Username 3";
            Guid userId3 = await CreateUserAsync(ct, username3);

            var server = await chatServerService.CreateChatServerAsync(new CreateChatServerRequest { Name = "test1" }, userId, ct);

            // Invite member
            var invitation2 = await chatServerService.InviteMemberAsync(
                new InviteServerMemberRequest
                {
                    ChatServerId = server.Id,
                    Username = username2,
                }, userId, ct);

            // Accepts Invitation
            await chatServerService.UpdateServerInvitationAsync(
                new UpdateServerInvitationRequest
                {
                    Id = invitation2.Id,
                    Status = ServerInvitationStatus.Accepted
                }, userId2, ct);

            await ValidateServerInvitationAcceptedAsync(memberRepository, invitationRepository, invitation2, userId, userId2, server.Id, ct);

            var invitation3 = await chatServerService.InviteMemberAsync(
                new InviteServerMemberRequest
                {
                    ChatServerId = server.Id,
                    Username = username3,
                }, userId2, ct);

            await ValidateServerInvitationPendingAsync(memberRepository, invitationRepository, invitation3, userId2, userId3, server.Id, ct);

            await chatServerService.UpdateServerInvitationAsync(
               new UpdateServerInvitationRequest
               {
                   Id = invitation3.Id,
                   Status = ServerInvitationStatus.Accepted
               }, userId3, ct);

            await ValidateServerInvitationAcceptedAsync(memberRepository, invitationRepository, invitation3, userId2, userId3, server.Id, ct);
        }

        [Fact]
        public async Task InviteUserToAServer_RejectAcceptedInvitation()
        {
            var ct = TestContext.Current.CancellationToken;


            var username1 = "Username 1";
            Guid userId = await CreateUserAsync(ct, username1);

            var username2 = "Username 2";
            Guid userId2 = await CreateUserAsync(ct, username2);
            var server = await chatServerService.CreateChatServerAsync(new CreateChatServerRequest { Name = "test1" }, userId, ct);

            // Invite member
            var invitation = await chatServerService.InviteMemberAsync(
                new InviteServerMemberRequest
                {
                    ChatServerId = server.Id,
                    Username = username2,
                }, userId, ct);

            // Accepts Invitation
            await chatServerService.UpdateServerInvitationAsync(
                new UpdateServerInvitationRequest
                {
                    Id = invitation.Id,
                    Status = ServerInvitationStatus.Accepted
                }, userId2, ct);

            // Validates invitation is accepted
            await ValidateServerInvitationAcceptedAsync(memberRepository, invitationRepository, invitation, userId, userId2, server.Id, ct);

            //Rejects accepted invitation

            await Assert.ThrowsAnyAsync<Exception>(async () => await chatServerService.UpdateServerInvitationAsync(
                new UpdateServerInvitationRequest
                {
                    Id = invitation.Id,
                    Status = ServerInvitationStatus.Rejected
                }, userId2, ct));

            // Validates Invitation is still accepted
            await ValidateServerInvitationAcceptedAsync(memberRepository, invitationRepository, invitation, userId, userId2, server.Id, ct);
        }

        [Fact]
        public async Task InviteUserToAServer_UserRejects()
        {
            var ct = TestContext.Current.CancellationToken;


            var username1 = "Username 1";
            Guid userId = await CreateUserAsync(ct, username1);

            var username2 = "Username 2";
            Guid userId2 = await CreateUserAsync(ct, username2);
            var server = await chatServerService.CreateChatServerAsync(new CreateChatServerRequest { Name = "test1" }, userId, ct);

            // Invite member
            var invitation = await chatServerService.InviteMemberAsync(
                new InviteServerMemberRequest
                {
                    ChatServerId = server.Id,
                    Username = username2,
                }, userId, ct);

            // Rejects Invitation
            await chatServerService.UpdateServerInvitationAsync(
                new UpdateServerInvitationRequest
                {
                    Id = invitation.Id,
                    Status = ServerInvitationStatus.Rejected
                }, userId2, ct);

            // Validate rejection and user is not a member
            await ValidateServerInvitationRejectedAsync(memberRepository, invitationRepository, invitation, userId, userId2, server.Id, ct);

            var servers = await chatServerService.GetServersByUserAsync(userId2, ct);
            servers.Should().BeEmpty();
        }

        [Fact]
        public async Task InviteUserToAServer_UserRejectsAndGetsInvitedAgain_AndAccepts()
        {
            var ct = TestContext.Current.CancellationToken;


            var username1 = "Username 1";
            Guid userId = await CreateUserAsync(ct, username1);

            var username2 = "Username 2";
            Guid userId2 = await CreateUserAsync(ct, username2);
            var server = await chatServerService.CreateChatServerAsync(new CreateChatServerRequest { Name = "test1" }, userId, ct);

            // Invite member
            var invitation = await chatServerService.InviteMemberAsync(
                new InviteServerMemberRequest
                {
                    ChatServerId = server.Id,
                    Username = username2,
                }, userId, ct);

            // Rejects Invitation
            await chatServerService.UpdateServerInvitationAsync(
                new UpdateServerInvitationRequest
                {
                    Id = invitation.Id,
                    Status = ServerInvitationStatus.Rejected
                }, userId2, ct);

            // Validates rejected invitation
            await ValidateServerInvitationRejectedAsync(memberRepository, invitationRepository, invitation, userId, userId2, server.Id, ct);

            // Invites Member again
            var invitation2 = await chatServerService.InviteMemberAsync(
                new InviteServerMemberRequest
                {
                    ChatServerId = server.Id,
                    Username = username2,
                }, userId, ct);

            invitation2.Should().NotBeNull();
            invitation2.Id.Should().NotBe(invitation.Id);

            // Accepts second Invitation
            await chatServerService.UpdateServerInvitationAsync(
                new UpdateServerInvitationRequest
                {
                    Id = invitation2.Id,
                    Status = ServerInvitationStatus.Accepted
                }, userId2, ct);

            // Validates second invitation is accepted 
            await ValidateServerInvitationAcceptedAsync(memberRepository, invitationRepository, invitation2, userId, userId2, server.Id, ct);
        }

        [Fact]
        public async Task InviteUserToAServer_UserAcceptsRejectedInvitation_Invalid()
        {
            var ct = TestContext.Current.CancellationToken;


            var username1 = "Username 1";
            Guid userId = await CreateUserAsync(ct, username1);

            var username2 = "Username 2";
            Guid userId2 = await CreateUserAsync(ct, username2);
            var server = await chatServerService.CreateChatServerAsync(new CreateChatServerRequest { Name = "test1" }, userId, ct);

            // Invite member
            var invitation = await chatServerService.InviteMemberAsync(
                new InviteServerMemberRequest
                {
                    ChatServerId = server.Id,
                    Username = username2,
                }, userId, ct);

            // Rejects Invitation
            await chatServerService.UpdateServerInvitationAsync(
                new UpdateServerInvitationRequest
                {
                    Id = invitation.Id,
                    Status = ServerInvitationStatus.Rejected
                }, userId2, ct);

            // Accept same Invitation
            await Assert.ThrowsAnyAsync<Exception>(async () => await chatServerService.UpdateServerInvitationAsync(
                new UpdateServerInvitationRequest
                {
                    Id = invitation.Id,
                    Status = ServerInvitationStatus.Accepted
                }, userId2, ct));

            // Validates Invitation is still rejected
            await ValidateServerInvitationRejectedAsync(memberRepository, invitationRepository, invitation, userId, userId2, server.Id, ct);
        }

        [Fact]
        public async Task InviteUserToAServer_UserRejectsAcceptedInvitation_Invalid()
        {
            var ct = TestContext.Current.CancellationToken;


            var username1 = "Username 1";
            Guid userId = await CreateUserAsync(ct, username1);

            var username2 = "Username 2";
            Guid userId2 = await CreateUserAsync(ct, username2);
            var server = await chatServerService.CreateChatServerAsync(new CreateChatServerRequest { Name = "test1" }, userId, ct);

            // Invite member
            var invitation = await chatServerService.InviteMemberAsync(
                new InviteServerMemberRequest
                {
                    ChatServerId = server.Id,
                    Username = username2,
                }, userId, ct);

            // Accepts Invitation
            await chatServerService.UpdateServerInvitationAsync(
                 new UpdateServerInvitationRequest
                 {
                     Id = invitation.Id,
                     Status = ServerInvitationStatus.Accepted
                 }, userId2, ct);

            // Rejects sane Invitation
            await Assert.ThrowsAnyAsync<ValidationException>(async () => await chatServerService.UpdateServerInvitationAsync(
                new UpdateServerInvitationRequest
                {
                    Id = invitation.Id,
                    Status = ServerInvitationStatus.Rejected
                }, userId2, ct));

            // Validates Invitation is still accepted 
            await ValidateServerInvitationAcceptedAsync(memberRepository, invitationRepository, invitation, userId, userId2, server.Id, ct);
        }

        [Fact]
        public async Task InviteUserToAServer_MemberRevokes()
        {
            var ct = TestContext.Current.CancellationToken;


            var username1 = "Username 1";
            Guid userId = await CreateUserAsync(ct, username1);

            var username2 = "Username 2";
            Guid userId2 = await CreateUserAsync(ct, username2);
            var server = await chatServerService.CreateChatServerAsync(new CreateChatServerRequest { Name = "test1" }, userId, ct);

            // Invite member
            var invitation = await chatServerService.InviteMemberAsync(
                new InviteServerMemberRequest
                {
                    ChatServerId = server.Id,
                    Username = username2,
                }, userId, ct);

            // Revokes Invitation
            await chatServerService.UpdateServerInvitationAsync(
                new UpdateServerInvitationRequest
                {
                    Id = invitation.Id,
                    Status = ServerInvitationStatus.Revoked
                }, userId, ct);

            // Validate rejection and user is not a member
            await ValidateServerInvitationRevokedAsync(memberRepository, invitationRepository, invitation, userId, userId2, server.Id, ct);

            var servers = await chatServerService.GetServersByUserAsync(userId2, ct);
            servers.Should().BeEmpty();
        }

        [Fact]
        public async Task InviteUserToAServer_InvitedUserRevokes()
        {
            var ct = TestContext.Current.CancellationToken;


            var username1 = "Username 1";
            Guid userId = await CreateUserAsync(ct, username1);

            var username2 = "Username 2";
            Guid userId2 = await CreateUserAsync(ct, username2);
            var server = await chatServerService.CreateChatServerAsync(new CreateChatServerRequest { Name = "test1" }, userId, ct);

            // Invite member
            var invitation = await chatServerService.InviteMemberAsync(
                new InviteServerMemberRequest
                {
                    ChatServerId = server.Id,
                    Username = username2,
                }, userId, ct);

            // Revokes Invitation
            await chatServerService.UpdateServerInvitationAsync(
                new UpdateServerInvitationRequest
                {
                    Id = invitation.Id,
                    Status = ServerInvitationStatus.Revoked
                }, userId, ct);
        }

        [Fact]
        public async Task InviteUserToAServer_InvitedUserRevokesItsInvitation()
        {
            var ct = TestContext.Current.CancellationToken;

            var username1 = "Username 1";
            Guid userId = await CreateUserAsync(ct, username1);

            var username2 = "Username 2";
            Guid userId2 = await CreateUserAsync(ct, username2);

            var server = await chatServerService.CreateChatServerAsync(new CreateChatServerRequest { Name = "test1" }, userId, ct);

            // Invite member
            var invitation = await chatServerService.InviteMemberAsync(
                new InviteServerMemberRequest
                {
                    ChatServerId = server.Id,
                    Username = username2,
                }, userId, ct);

            // Revokes its own Invitation
            await Assert.ThrowsAsync<ValidationException>(async () => await chatServerService.UpdateServerInvitationAsync(
                new UpdateServerInvitationRequest
                {
                    Id = invitation.Id,
                    Status = ServerInvitationStatus.Revoked
                }, userId2, ct));

            await ValidateServerInvitationPendingAsync(memberRepository, invitationRepository, invitation, userId, userId2, server.Id, ct);
        }

        [Fact]
        public async Task ServerMembers_GetMembersByServer_Sorting()
        {
            var ct = TestContext.Current.CancellationToken;

            var username1 = "Username 1";
            Guid userId = await CreateUserAsync(ct, username1);

            var server = await chatServerService.CreateChatServerAsync(new CreateChatServerRequest { Name = "test1" }, userId, ct);

            var users = new List<UserDto>();

            int membersCount = 30;
            for (int i = 0; i < membersCount; i++)
            {
                users.Add(await CreateUserDtoAsync(ct,  $"{i}-{Guid.NewGuid().ToString()}"));
            }

            // Shuffle users 
            users = users.Shuffle().ToList();

            foreach (var user in users)
            {
                // Invite User
                var invitation = await chatServerService.InviteMemberAsync(
                new InviteServerMemberRequest
                {
                    ChatServerId = server.Id,
                    Username = user.Username!,
                }, userId, ct);

                // Accept Invitation

                await chatServerService.UpdateServerInvitationAsync(
                new UpdateServerInvitationRequest
                {
                    Id = invitation.Id,
                    Status = ServerInvitationStatus.Accepted
                }, user.Id, ct);
            }

            // Get sorted membersByName 
            var members = await chatServerService.GetServerMemberDtosAsync(server.Id, userId, new PageRequest
            {
                Descending = false,
                PageSize = membersCount,
                SortBy = "username"
            }, ct);
            members.Should().NotBeNull();
            members.Items.Should().NotBeNullOrEmpty();
            members.Items.Should().HaveCount(membersCount);

            var sortedUsernames = users.Select(x => x.Username).OrderBy(x => x!, StringComparer.InvariantCultureIgnoreCase).ToList();  
            sortedUsernames.Should().ContainInConsecutiveOrder(members.Items.Select(x => x.Username).ToList());

            // Test Pagination 
            Guid? next = null;

            int pageSize = 5;
            var pagedMembers = new List<ServerMemberDto>();
            do
            {
                var page = await chatServerService.GetServerMemberDtosAsync(server.Id, userId, new PageRequest
                {
                    Next = next,
                    Descending = false,
                    PageSize = pageSize,
                    SortBy = "username"
                }, ct);

                if (page.Next != null)
                {
                    page.Items.Should().NotBeNullOrEmpty();
                    page.Items.Count.Should().Be(pageSize);
                    page.PageSize.Should().Be(pageSize);
                }

                pagedMembers.AddRange(page.Items);
                next = page.Next;

            } while (next != null);

            pagedMembers.Should().NotBeEmpty();
            pagedMembers.Select(x=> x.Username).ToList().Should().ContainInConsecutiveOrder(sortedUsernames);

        }

        /* Pending revoked edge test cases*/

        private static async Task ValidateServerInvitationAcceptedAsync(IServerMemberRepository memberRepository, IServerInvitationRepository invitationRepository, ServerInvitationDto invitation, Guid invitedBy, Guid userId, Guid serverId, CancellationToken ct)
        {
            var dbInvitation = await invitationRepository.GetServerInvitationByIdAsync(invitation.Id, ct);
            dbInvitation.Should().NotBeNull();
            dbInvitation.Id.Should().Be(invitation.Id);
            dbInvitation.UserId.Should().Be(userId);
            dbInvitation.UserId.Should().Be(invitation.UserId);
            dbInvitation.InvitedBy.Should().Be(invitedBy);
            dbInvitation.Status.Should().Be(ServerInvitationStatus.Accepted);

            var member = await memberRepository.GetServerMemberAsync(serverId, userId, ct);
            member.Should().NotBeNull();
            member.ChatServerId.Should().Be(serverId);
            member.UserId.Should().Be(userId);
            member.InvitationId.Should().Be(invitation.Id);
        }

        private static async Task ValidateServerInvitationRejectedAsync(IServerMemberRepository memberRepository, IServerInvitationRepository invitationRepository, ServerInvitationDto invitation, Guid invitedBy, Guid userId, Guid serverId, CancellationToken ct)
        {
            var dbInvitation = await invitationRepository.GetServerInvitationByIdAsync(invitation.Id, ct);
            dbInvitation.Should().NotBeNull();
            dbInvitation.Id.Should().Be(invitation.Id);
            dbInvitation.UserId.Should().Be(userId);
            dbInvitation.UserId.Should().Be(invitation.UserId);
            dbInvitation.InvitedBy.Should().Be(invitedBy);
            dbInvitation.Status.Should().Be(ServerInvitationStatus.Rejected);

            var member = await memberRepository.GetServerMemberAsync(serverId, userId, ct);
            member.Should().BeNull();
        }
        private static async Task ValidateServerInvitationRevokedAsync(IServerMemberRepository memberRepository, IServerInvitationRepository invitationRepository, ServerInvitationDto invitation, Guid invitedBy, Guid userId, Guid serverId, CancellationToken ct)
        {
            var dbInvitation = await invitationRepository.GetServerInvitationByIdAsync(invitation.Id, ct);
            dbInvitation.Should().NotBeNull();
            dbInvitation.Id.Should().Be(invitation.Id);
            dbInvitation.UserId.Should().Be(userId);
            dbInvitation.UserId.Should().Be(invitation.UserId);
            dbInvitation.InvitedBy.Should().Be(invitedBy);
            dbInvitation.Status.Should().Be(ServerInvitationStatus.Revoked);

            var member = await memberRepository.GetServerMemberAsync(serverId, userId, ct);
            member.Should().BeNull();
        }

        private static async Task ValidateServerInvitationPendingAsync(IServerMemberRepository memberRepository, IServerInvitationRepository invitationRepository, ServerInvitationDto invitation, Guid invitedBy, Guid userId, Guid serverId, CancellationToken ct)
        {
            var dbInvitation = await invitationRepository.GetServerInvitationByIdAsync(invitation.Id, ct);
            dbInvitation.Should().NotBeNull();
            dbInvitation.Id.Should().Be(invitation.Id);
            dbInvitation.UserId.Should().Be(userId);
            dbInvitation.UserId.Should().Be(invitation.UserId);
            dbInvitation.InvitedBy.Should().Be(invitedBy);
            dbInvitation.Status.Should().Be(ServerInvitationStatus.Pending);

            var member = await memberRepository.GetServerMemberAsync(serverId, userId, ct);
            member.Should().BeNull();
        }

        private async Task<UserDto> CreateUserDtoAsync(CancellationToken ct, string? username = null)
        {
            var userService = container.Resolve<IUserService>();
            var random = Guid.NewGuid().ToString();
            return (await userService.CreateUserAsync(new CreateUserRequest
            {
                Email = random,
                Username = username ?? random,
                Password = random
            }));
        }

        private async Task<Guid> CreateUserAsync(CancellationToken ct, string? username = null)
        {
            return (await CreateUserDtoAsync(ct, username)).Id;
        }
    }
}
