using Autofac;
using Dapper;
using FluentAssertions;
using HootOut.CommonDomain.DefaultValues;
using HootOut.CommonDomain.Persistence;
using HootOut.CommonIntegrationTests.PostgreSQL;
using HootOut.CommonIntegrationTests.S3;
using HootOut.Contracts.Common.Enums;
using HootOut.Contracts.Common.Saver;
using HootOut.Friends.Entities;
using HootOut.Infraestructure.DI;
using HootOut.Users.Entities;
using Npgsql;

namespace HootOut.PostgreSQL.IntegrationTests.Savers.Friends
{
    [Collection("IntegrationTests")]
    public class FriendRequestSaverTests : IClassFixture<PostgresTestContainer>, IClassFixture<TestS3Container>, IAsyncLifetime
    {
        private static readonly DateTime BaseDate = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        private readonly IContainer container;

        private readonly ClearAllTables clearTables;

        private readonly ISaver<FriendRequest> saver;

        private readonly ISaver<UserInfo> userSaver;

        private readonly IPersistenceProvider provider;

        public FriendRequestSaverTests(PostgresTestContainer postgresContainer)
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

            saver = container.Resolve<ISaver<FriendRequest>>();
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

        [Fact]
        public async Task SaveAsync_NewRequest_PersistsAllColumns()
        {
            var ct = TestContext.Current.CancellationToken;
            var sender = await CreateUserAsync("sender", ct);
            var receiver = await CreateUserAsync("receiver", ct);
            var request = BuildRequest(sender.Id, receiver.Id, InvitationStatus.Pending);

            await saver.SaveAsync(request, ct);

            var row = await GetRowAsync(request.Id, ct);
            row.Should().NotBeNull();
            row.Id.Should().Be(request.Id);
            row.InvitedBy.Should().Be(sender.Id);
            row.UserId.Should().Be(receiver.Id);
            row.Status.Should().Be((short)InvitationStatus.Pending);
            // timestamptz stores microseconds while DateTime has 100 ns ticks
            row.CreatedAt.Should().BeCloseTo(request.CreatedAt, TimeSpan.FromMilliseconds(1));
            row.ModifiedAt.Should().BeCloseTo(request.ModifiedAt, TimeSpan.FromMilliseconds(1));
        }

        [Theory]
        [InlineData(InvitationStatus.Pending)]
        [InlineData(InvitationStatus.Accepted)]
        [InlineData(InvitationStatus.Rejected)]
        [InlineData(InvitationStatus.Revoked)]
        public async Task SaveAsync_AnyValidStatus_Persists(InvitationStatus status)
        {
            var ct = TestContext.Current.CancellationToken;
            var sender = await CreateUserAsync("sender", ct);
            var receiver = await CreateUserAsync("receiver", ct);
            var request = BuildRequest(sender.Id, receiver.Id, status);

            await saver.SaveAsync(request, ct);

            (await GetRowAsync(request.Id, ct))!.Status.Should().Be((short)status);
        }

        [Fact]
        public async Task SaveAsync_InvalidStatus_ThrowsWrappedCheckViolation()
        {
            var ct = TestContext.Current.CancellationToken;
            var sender = await CreateUserAsync("sender", ct);
            var receiver = await CreateUserAsync("receiver", ct);
            var request = BuildRequest(sender.Id, receiver.Id, (InvitationStatus)5);

            var act = () => saver.SaveAsync(request, ct);

            var exception = await act.Should().ThrowExactlyAsync<Exception>()
                .WithMessage($"*{request.Id}*");
            exception.WithInnerException<PostgresException>()
                .Which.SqlState.Should().Be(PostgresErrorCodes.CheckViolation);
            (await CountRowsAsync(ct)).Should().Be(0);
        }

        [Fact]
        public async Task SaveAsync_RequestToYourself_ThrowsWrappedCheckViolation()
        {
            var ct = TestContext.Current.CancellationToken;
            var user = await CreateUserAsync("user", ct);
            var request = BuildRequest(user.Id, user.Id, InvitationStatus.Pending);

            var act = () => saver.SaveAsync(request, ct);

            var exception = await act.Should().ThrowExactlyAsync<Exception>()
                .WithMessage($"*{request.Id}*");
            exception.WithInnerException<PostgresException>()
                .Which.ConstraintName.Should().Be("ck_friend_request_not_self");
            (await CountRowsAsync(ct)).Should().Be(0);
        }

        [Fact]
        public async Task SaveAsync_NotExistingSender_ThrowsWrappedForeignKeyViolation()
        {
            var ct = TestContext.Current.CancellationToken;
            var receiver = await CreateUserAsync("receiver", ct);
            var request = BuildRequest(Guid.NewGuid(), receiver.Id, InvitationStatus.Pending);

            var act = () => saver.SaveAsync(request, ct);

            var exception = await act.Should().ThrowExactlyAsync<Exception>()
                .WithMessage($"*{request.Id}*");
            exception.WithInnerException<PostgresException>()
                .Which.SqlState.Should().Be(PostgresErrorCodes.ForeignKeyViolation);
            (await CountRowsAsync(ct)).Should().Be(0);
        }

        [Fact]
        public async Task SaveAsync_NotExistingReceiver_ThrowsWrappedForeignKeyViolation()
        {
            var ct = TestContext.Current.CancellationToken;
            var sender = await CreateUserAsync("sender", ct);
            var request = BuildRequest(sender.Id, Guid.NewGuid(), InvitationStatus.Pending);

            var act = () => saver.SaveAsync(request, ct);

            var exception = await act.Should().ThrowExactlyAsync<Exception>()
                .WithMessage($"*{request.Id}*");
            exception.WithInnerException<PostgresException>()
                .Which.SqlState.Should().Be(PostgresErrorCodes.ForeignKeyViolation);
            (await CountRowsAsync(ct)).Should().Be(0);
        }

        #endregion

        #region Pending requests per pair

        [Fact]
        public async Task SaveAsync_SecondPendingRequestSameDirection_ThrowsWrappedUniqueViolation()
        {
            var ct = TestContext.Current.CancellationToken;
            var sender = await CreateUserAsync("sender", ct);
            var receiver = await CreateUserAsync("receiver", ct);
            await saver.SaveAsync(BuildRequest(sender.Id, receiver.Id, InvitationStatus.Pending), ct);
            var duplicate = BuildRequest(sender.Id, receiver.Id, InvitationStatus.Pending);

            var act = () => saver.SaveAsync(duplicate, ct);

            var exception = await act.Should().ThrowExactlyAsync<Exception>()
                .WithMessage($"*{duplicate.Id}*");
            exception.WithInnerException<PostgresException>()
                .Which.ConstraintName.Should().Be("ux_friend_request_pending");
            (await CountRowsAsync(ct)).Should().Be(1);
        }

        [Fact]
        public async Task SaveAsync_PendingRequestInOppositeDirection_ThrowsWrappedUniqueViolation()
        {
            var ct = TestContext.Current.CancellationToken;
            var first = await CreateUserAsync("first", ct);
            var second = await CreateUserAsync("second", ct);
            await saver.SaveAsync(BuildRequest(first.Id, second.Id, InvitationStatus.Pending), ct);
            var reverse = BuildRequest(second.Id, first.Id, InvitationStatus.Pending);

            var act = () => saver.SaveAsync(reverse, ct);

            // If A already invited B, B has to accept that request instead of opening a new one
            var exception = await act.Should().ThrowExactlyAsync<Exception>()
                .WithMessage($"*{reverse.Id}*");
            exception.WithInnerException<PostgresException>()
                .Which.ConstraintName.Should().Be("ux_friend_request_pending");
            (await CountRowsAsync(ct)).Should().Be(1);
        }

        [Theory]
        [InlineData(InvitationStatus.Accepted)]
        [InlineData(InvitationStatus.Rejected)]
        [InlineData(InvitationStatus.Revoked)]
        public async Task SaveAsync_PendingAfterClosedRequest_Persists(InvitationStatus previousStatus)
        {
            var ct = TestContext.Current.CancellationToken;
            var sender = await CreateUserAsync("sender", ct);
            var receiver = await CreateUserAsync("receiver", ct);
            await saver.SaveAsync(BuildRequest(sender.Id, receiver.Id, previousStatus), ct);

            // The unique index only covers pending requests
            await saver.SaveAsync(BuildRequest(sender.Id, receiver.Id, InvitationStatus.Pending), ct);
            await saver.SaveAsync(BuildRequest(receiver.Id, sender.Id, previousStatus), ct);

            (await CountRowsAsync(ct)).Should().Be(3);
        }

        [Fact]
        public async Task SaveAsync_PendingRequestsWithDifferentUsers_PersistsAll()
        {
            var ct = TestContext.Current.CancellationToken;
            var user = await CreateUserAsync("user", ct);
            var first = await CreateUserAsync("first", ct);
            var second = await CreateUserAsync("second", ct);

            await saver.SaveAsync(BuildRequest(user.Id, first.Id, InvitationStatus.Pending), ct);
            await saver.SaveAsync(BuildRequest(user.Id, second.Id, InvitationStatus.Pending), ct);
            await saver.SaveAsync(BuildRequest(first.Id, second.Id, InvitationStatus.Pending), ct);

            (await CountRowsAsync(ct)).Should().Be(3);
        }

        #endregion

        #region Update

        [Theory]
        [InlineData(InvitationStatus.Accepted)]
        [InlineData(InvitationStatus.Rejected)]
        [InlineData(InvitationStatus.Revoked)]
        public async Task SaveAsync_ExistingRequest_UpdatesStatusAndModifiedAtOnly(InvitationStatus newStatus)
        {
            var ct = TestContext.Current.CancellationToken;
            var sender = await CreateUserAsync("sender", ct);
            var receiver = await CreateUserAsync("receiver", ct);
            var other = await CreateUserAsync("other", ct);
            var original = BuildRequest(sender.Id, receiver.Id, InvitationStatus.Pending);
            await saver.SaveAsync(original, ct);

            var updated = new FriendRequest
            {
                Id = original.Id,
                InvitedBy = other.Id,
                UserId = sender.Id,
                Status = newStatus,
                CreatedAt = original.CreatedAt.AddDays(-10),
                ModifiedAt = original.ModifiedAt.AddDays(1)
            };

            await saver.SaveAsync(updated, ct);

            var row = await GetRowAsync(original.Id, ct);
            row.Should().NotBeNull();
            row.Status.Should().Be((short)newStatus);
            row.ModifiedAt.Should().BeCloseTo(updated.ModifiedAt, TimeSpan.FromMilliseconds(1));
            // Sender, receiver and creation date never change
            row.InvitedBy.Should().Be(sender.Id);
            row.UserId.Should().Be(receiver.Id);
            row.CreatedAt.Should().BeCloseTo(original.CreatedAt, TimeSpan.FromMilliseconds(1));
            (await CountRowsAsync(ct)).Should().Be(1);
        }

        [Fact]
        public async Task SaveAsync_RejectedRequestThenNewPending_Persists()
        {
            var ct = TestContext.Current.CancellationToken;
            var sender = await CreateUserAsync("sender", ct);
            var receiver = await CreateUserAsync("receiver", ct);
            var request = BuildRequest(sender.Id, receiver.Id, InvitationStatus.Pending);
            await saver.SaveAsync(request, ct);

            request.Status = InvitationStatus.Rejected;
            await saver.SaveAsync(request, ct);
            await saver.SaveAsync(BuildRequest(receiver.Id, sender.Id, InvitationStatus.Pending), ct);

            (await CountRowsAsync(ct)).Should().Be(2);
        }

        [Fact]
        public async Task SaveAsync_ReopenRequestWhileAnotherIsPending_ThrowsWrappedUniqueViolation()
        {
            var ct = TestContext.Current.CancellationToken;
            var sender = await CreateUserAsync("sender", ct);
            var receiver = await CreateUserAsync("receiver", ct);
            var rejected = BuildRequest(sender.Id, receiver.Id, InvitationStatus.Rejected);
            await saver.SaveAsync(rejected, ct);
            await saver.SaveAsync(BuildRequest(receiver.Id, sender.Id, InvitationStatus.Pending), ct);

            rejected.Status = InvitationStatus.Pending;
            var act = () => saver.SaveAsync(rejected, ct);

            var exception = await act.Should().ThrowExactlyAsync<Exception>()
                .WithMessage($"*{rejected.Id}*");
            exception.WithInnerException<PostgresException>()
                .Which.ConstraintName.Should().Be("ux_friend_request_pending");
            (await GetRowAsync(rejected.Id, ct))!.Status.Should().Be((short)InvitationStatus.Rejected);
        }

        #endregion

        [Fact]
        public async Task SaveAsync_CancelledToken_ThrowsOperationCanceledAndDoesNotPersist()
        {
            var sender = await CreateUserAsync("sender", TestContext.Current.CancellationToken);
            var receiver = await CreateUserAsync("receiver", TestContext.Current.CancellationToken);
            var request = BuildRequest(sender.Id, receiver.Id, InvitationStatus.Pending);
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            var act = () => saver.SaveAsync(request, cts.Token);

            // The token is already cancelled when opening the connection, which happens outside the try/catch
            await act.Should().ThrowAsync<OperationCanceledException>();
            (await CountRowsAsync(TestContext.Current.CancellationToken)).Should().Be(0);
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

        private static FriendRequest BuildRequest(Guid invitedBy, Guid userId, InvitationStatus status)
        {
            var request = Persistable.CreateNew<FriendRequest>();
            request.InvitedBy = invitedBy;
            request.UserId = userId;
            request.Status = status;
            request.CreatedAt = BaseDate;
            request.ModifiedAt = BaseDate;
            return request;
        }

        private async Task<FriendRequestRow?> GetRowAsync(Guid id, CancellationToken ct)
        {
            await using var conn = await provider.OpenConnectionAsync(ct);
            return await conn.QuerySingleOrDefaultAsync<FriendRequestRow>(new CommandDefinition(
                """
                    SELECT "Id", "InvitedBy", "UserId", "Status", "CreatedAt", "ModifiedAt"
                    FROM "HootOut"."FriendRequest"
                    WHERE "Id" = @Id
                """,
                new { Id = id },
                cancellationToken: ct));
        }

        private async Task<long> CountRowsAsync(CancellationToken ct)
        {
            await using var conn = await provider.OpenConnectionAsync(ct);
            return await conn.ExecuteScalarAsync<long>(new CommandDefinition(
                """SELECT COUNT(*) FROM "HootOut"."FriendRequest" """,
                cancellationToken: ct));
        }

        /// <summary>
        /// Raw table row; Status is read as its smallint value.
        /// </summary>
        private sealed class FriendRequestRow
        {
            public Guid Id { get; set; }
            public Guid InvitedBy { get; set; }
            public Guid UserId { get; set; }
            public short Status { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTime ModifiedAt { get; set; }
        }
    }
}
