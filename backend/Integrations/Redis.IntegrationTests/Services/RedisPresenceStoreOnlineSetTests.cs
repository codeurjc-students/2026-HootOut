using FluentAssertions;
using HootOut.CommonIntegrationTests.Services;
using HootOut.Contracts.Common.Presence;
using HootOut.Redis.Services;
using StackExchange.Redis;

namespace HootOut.Redis.IntegrationTests.Services
{
    /// <summary>The set <c>{presence}:online</c>, kept by the Lua scripts, and the lookups that use it.</summary>
    public class RedisPresenceStoreOnlineSetTests : IClassFixture<TestContainerRedisConnectionFactory>, IAsyncLifetime
    {
        private readonly TestContainerRedisConnectionFactory redisConnectionFactory;
        private readonly IConnectionMultiplexer redis;
        private readonly PresenceOptions options = new() { InstanceTtl = TimeSpan.FromSeconds(30) };

        private readonly PresenceInstance instanceA = new();
        private readonly PresenceInstance instanceB = new();
        private readonly RedisPresenceStore storeA;
        private readonly RedisPresenceStore storeB;

        public RedisPresenceStoreOnlineSetTests(TestContainerRedisConnectionFactory factory)
        {
            redisConnectionFactory = factory;
            redis = factory.Connect();
            storeA = new RedisPresenceStore(redis, instanceA, options);
            storeB = new RedisPresenceStore(redis, instanceB, options);
        }

        public async ValueTask InitializeAsync()
        {
            await redisConnectionFactory.FlushAsync();
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private IDatabase Db => redis.GetDatabase();

        private async Task<string[]> OnlineSetAsync() =>
            (await Db.SetMembersAsync(RedisPresenceStore.OnlineKey)).Select(x => x.ToString()).ToArray();

        #region Keys

        [Fact]
        public void UserKeyAndOnlineKey_ShareTheHashTag()
        {
            // Required by the scripts in cluster mode: both keys must be in the same slot
            RedisPresenceStore.UserKey(Guid.NewGuid()).ToString().Should().StartWith("{presence}:");
            RedisPresenceStore.OnlineKey.ToString().Should().StartWith("{presence}:");
        }

        #endregion

        #region Add / Remove

        [Fact]
        public async Task AddConnection_FirstConnection_AddsUserToOnlineSet()
        {
            var ct = TestContext.Current.CancellationToken;
            var userId = Guid.NewGuid();

            await storeA.AddConnectionAsync(userId, Guid.NewGuid(), ct);

            (await OnlineSetAsync()).Should().Equal(userId.ToString());
        }

        [Fact]
        public async Task AddConnection_SecondConnectionOtherInstance_KeepsOneEntry()
        {
            var ct = TestContext.Current.CancellationToken;
            var userId = Guid.NewGuid();

            await storeA.AddConnectionAsync(userId, Guid.NewGuid(), ct);
            (await storeB.AddConnectionAsync(userId, Guid.NewGuid(), ct)).Should().BeFalse();

            (await OnlineSetAsync()).Should().Equal(userId.ToString());
        }

        [Fact]
        public async Task AddConnection_UserMissingFromOnlineSet_RepairsIt()
        {
            var ct = TestContext.Current.CancellationToken;
            var userId = Guid.NewGuid();
            await storeA.AddConnectionAsync(userId, Guid.NewGuid(), ct);
            // E.g. the set was lost while the user was connected
            await Db.KeyDeleteAsync(RedisPresenceStore.OnlineKey);

            (await storeA.AddConnectionAsync(userId, Guid.NewGuid(), ct)).Should().BeFalse();

            (await OnlineSetAsync()).Should().Equal(userId.ToString());
        }

        [Fact]
        public async Task RemoveConnection_NotTheLast_KeepsUserInOnlineSet()
        {
            var ct = TestContext.Current.CancellationToken;
            var userId = Guid.NewGuid();
            var first = Guid.NewGuid();
            await storeA.AddConnectionAsync(userId, first, ct);
            await storeB.AddConnectionAsync(userId, Guid.NewGuid(), ct);

            (await storeA.RemoveConnectionAsync(userId, first, ct)).Should().BeFalse();

            (await OnlineSetAsync()).Should().Equal(userId.ToString());
            (await storeA.IsOnlineAsync(userId, ct)).Should().BeTrue();
        }

        [Fact]
        public async Task RemoveConnection_Last_RemovesUserFromOnlineSet()
        {
            var ct = TestContext.Current.CancellationToken;
            var userId = Guid.NewGuid();
            var other = Guid.NewGuid();
            var connectionId = Guid.NewGuid();
            await storeA.AddConnectionAsync(userId, connectionId, ct);
            await storeA.AddConnectionAsync(other, Guid.NewGuid(), ct);

            (await storeA.RemoveConnectionAsync(userId, connectionId, ct)).Should().BeTrue();

            (await OnlineSetAsync()).Should().Equal(other.ToString());
            (await storeA.IsOnlineAsync(userId, ct)).Should().BeFalse();
        }

        [Fact]
        public async Task RemoveConnection_UserWithoutConnectionsLeftInOnlineSet_RemovesIt()
        {
            var ct = TestContext.Current.CancellationToken;
            var userId = Guid.NewGuid();
            // A stale entry without connections
            await Db.SetAddAsync(RedisPresenceStore.OnlineKey, userId.ToString());

            (await storeA.RemoveConnectionAsync(userId, Guid.NewGuid(), ct)).Should().BeFalse();

            (await OnlineSetAsync()).Should().BeEmpty();
        }

        [Fact]
        public async Task ConcurrentAddAndRemove_OnlineSetMatchesConnections()
        {
            var ct = TestContext.Current.CancellationToken;
            var users = Enumerable.Range(0, 20).Select(_ => Guid.NewGuid()).ToList();
            var connections = users.ToDictionary(u => u, _ => Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToList());

            await Task.WhenAll(connections.SelectMany(kv => kv.Value.Select((c, i) =>
                (i % 2 == 0 ? storeA : storeB).AddConnectionAsync(kv.Key, c, ct))));

            // Half of the users close every connection, the other half keep one
            var leaving = users.Take(10).ToHashSet();
            await Task.WhenAll(connections.SelectMany(kv => kv.Value
                .Select((c, i) => (c, i))
                .Where(x => leaving.Contains(kv.Key) || x.i > 0)
                .Select(x => (x.i % 2 == 0 ? storeA : storeB).RemoveConnectionAsync(kv.Key, x.c, ct))));

            (await OnlineSetAsync()).Should().BeEquivalentTo(users.Skip(10).Select(u => u.ToString()));
        }

        #endregion

        #region Sweep

        [Fact]
        public async Task Sweep_DeadInstance_RemovesItsUsersFromOnlineSet()
        {
            var ct = TestContext.Current.CancellationToken;
            var deadInstance = new PresenceInstance();
            var deadStore = new RedisPresenceStore(redis, deadInstance, options);
            await deadStore.RegisterInstanceAsync(ct);
            await storeA.RegisterInstanceAsync(ct);

            var onlyDead = Guid.NewGuid();
            var inBoth = Guid.NewGuid();
            await deadStore.AddConnectionAsync(onlyDead, Guid.NewGuid(), ct);
            await deadStore.AddConnectionAsync(inBoth, Guid.NewGuid(), ct);
            await storeA.AddConnectionAsync(inBoth, Guid.NewGuid(), ct);
            await Db.KeyDeleteAsync(RedisPresenceStore.AliveKey(deadInstance.Id));

            (await storeA.SweepDeadInstancesAsync(ct)).Should().Equal(onlyDead);

            (await OnlineSetAsync()).Should().Equal(inBoth.ToString());
        }

        #endregion

        #region GetOnlineUsers

        [Fact]
        public async Task GetOnlineUsers_ReadsTheOnlineSet()
        {
            var ct = TestContext.Current.CancellationToken;
            var online = Guid.NewGuid();
            var offline = Guid.NewGuid();
            await storeA.AddConnectionAsync(online, Guid.NewGuid(), ct);

            (await storeB.GetOnlineUsersAsync([online, offline], ct)).Should().Equal(online);
        }

        [Fact]
        public async Task GetOnlineUsers_MoreIdsThanOneChunk_ReturnsTheOnlineOnesOfEveryChunk()
        {
            var ct = TestContext.Current.CancellationToken;
            var ids = Enumerable.Range(0, 2500).Select(_ => Guid.NewGuid()).ToList();
            // One online user at the start, one at the end of each chunk of 1000 and one in the last chunk
            var online = new[] { ids[0], ids[999], ids[1000], ids[1999], ids[2499] };
            foreach (var userId in online)
            {
                await storeA.AddConnectionAsync(userId, Guid.NewGuid(), ct);
            }

            (await storeA.GetOnlineUsersAsync(ids, ct)).Should().BeEquivalentTo(online);
        }

        [Fact]
        public async Task GetOnlineUsers_CancelledToken_Throws()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var act = () => storeA.GetOnlineUsersAsync([Guid.NewGuid()], cts.Token);

            await act.Should().ThrowAsync<OperationCanceledException>();
        }

        #endregion
    }
}
