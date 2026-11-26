using FluentAssertions;
using HootOut.CommonIntegrationTests.Services;
using HootOut.Contracts.Common.Presence;
using HootOut.Redis.Services;
using StackExchange.Redis;

namespace HootOut.Redis.IntegrationTests.Services
{
    public class RedisPresenceStoreTests : IClassFixture<TestContainerRedisConnectionFactory>, IAsyncLifetime
    {
        private readonly TestContainerRedisConnectionFactory redisConnectionFactory;
        private readonly IConnectionMultiplexer redis;
        private readonly PresenceOptions options = new() { InstanceTtl = TimeSpan.FromSeconds(30) };

        // Two WebSocket instances sharing the same Valkey
        private readonly PresenceInstance instanceA = new();
        private readonly PresenceInstance instanceB = new();
        private readonly RedisPresenceStore storeA;
        private readonly RedisPresenceStore storeB;

        public RedisPresenceStoreTests(TestContainerRedisConnectionFactory factory)
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

        #region Add / Remove

        [Fact]
        public async Task AddConnection_FirstConnection_ReturnsTrueAndStoresBothKeys()
        {
            var ct = TestContext.Current.CancellationToken;
            var userId = Guid.NewGuid();
            var connectionId = Guid.NewGuid();

            (await storeA.AddConnectionAsync(userId, connectionId, ct)).Should().BeTrue();

            var userMembers = await Db.SetMembersAsync(RedisPresenceStore.UserKey(userId));
            userMembers.Select(x => x.ToString()).Should().Equal($"{instanceA.Id}:{connectionId}");

            var instanceMembers = await Db.SetMembersAsync(RedisPresenceStore.InstanceConnectionsKey(instanceA.Id));
            instanceMembers.Select(x => x.ToString()).Should().Equal($"{userId}:{connectionId}");

            (await storeA.IsOnlineAsync(userId, ct)).Should().BeTrue();
        }

        [Fact]
        public async Task AddConnection_SecondConnectionSameInstance_ReturnsFalse()
        {
            var ct = TestContext.Current.CancellationToken;
            var userId = Guid.NewGuid();

            (await storeA.AddConnectionAsync(userId, Guid.NewGuid(), ct)).Should().BeTrue();
            (await storeA.AddConnectionAsync(userId, Guid.NewGuid(), ct)).Should().BeFalse();

            (await Db.SetLengthAsync(RedisPresenceStore.UserKey(userId))).Should().Be(2);
        }

        [Fact]
        public async Task AddConnection_SecondConnectionOtherInstance_ReturnsFalse()
        {
            var ct = TestContext.Current.CancellationToken;
            var userId = Guid.NewGuid();

            (await storeA.AddConnectionAsync(userId, Guid.NewGuid(), ct)).Should().BeTrue();
            (await storeB.AddConnectionAsync(userId, Guid.NewGuid(), ct)).Should().BeFalse();

            (await Db.SetLengthAsync(RedisPresenceStore.UserKey(userId))).Should().Be(2);
        }

        [Fact]
        public async Task AddConnection_SameConnectionTwice_StoredOnceAndSecondReturnsFalse()
        {
            var ct = TestContext.Current.CancellationToken;
            var userId = Guid.NewGuid();
            var connectionId = Guid.NewGuid();

            (await storeA.AddConnectionAsync(userId, connectionId, ct)).Should().BeTrue();
            (await storeA.AddConnectionAsync(userId, connectionId, ct)).Should().BeFalse();

            (await Db.SetLengthAsync(RedisPresenceStore.UserKey(userId))).Should().Be(1);
        }

        [Fact]
        public async Task RemoveConnection_LastConnection_ReturnsTrueAndRemovesKeys()
        {
            var ct = TestContext.Current.CancellationToken;
            var userId = Guid.NewGuid();
            var connectionId = Guid.NewGuid();
            await storeA.AddConnectionAsync(userId, connectionId, ct);

            (await storeA.RemoveConnectionAsync(userId, connectionId, ct)).Should().BeTrue();

            (await storeA.IsOnlineAsync(userId, ct)).Should().BeFalse();
            // An empty set does not exist in Redis/Valkey
            (await Db.KeyExistsAsync(RedisPresenceStore.UserKey(userId))).Should().BeFalse();
            (await Db.SetLengthAsync(RedisPresenceStore.InstanceConnectionsKey(instanceA.Id))).Should().Be(0);
        }

        [Fact]
        public async Task RemoveConnection_NotLastConnection_ReturnsFalseAndKeepsOnline()
        {
            var ct = TestContext.Current.CancellationToken;
            var userId = Guid.NewGuid();
            var connection1 = Guid.NewGuid();
            var connection2 = Guid.NewGuid();
            await storeA.AddConnectionAsync(userId, connection1, ct);
            await storeB.AddConnectionAsync(userId, connection2, ct);

            (await storeA.RemoveConnectionAsync(userId, connection1, ct)).Should().BeFalse();
            (await storeA.IsOnlineAsync(userId, ct)).Should().BeTrue();

            (await storeB.RemoveConnectionAsync(userId, connection2, ct)).Should().BeTrue();
            (await storeA.IsOnlineAsync(userId, ct)).Should().BeFalse();
        }

        [Fact]
        public async Task RemoveConnection_UnknownConnection_ReturnsFalse()
        {
            var ct = TestContext.Current.CancellationToken;

            (await storeA.RemoveConnectionAsync(Guid.NewGuid(), Guid.NewGuid(), ct)).Should().BeFalse();
        }

        [Fact]
        public async Task RemoveConnection_Twice_OnlyFirstReturnsTrue()
        {
            var ct = TestContext.Current.CancellationToken;
            var userId = Guid.NewGuid();
            var connectionId = Guid.NewGuid();
            await storeA.AddConnectionAsync(userId, connectionId, ct);

            (await storeA.RemoveConnectionAsync(userId, connectionId, ct)).Should().BeTrue();
            (await storeA.RemoveConnectionAsync(userId, connectionId, ct)).Should().BeFalse();
        }

        [Fact]
        public async Task RemoveConnection_FromOtherInstance_DoesNotRemoveIt()
        {
            var ct = TestContext.Current.CancellationToken;
            var userId = Guid.NewGuid();
            var connectionId = Guid.NewGuid();
            await storeA.AddConnectionAsync(userId, connectionId, ct);

            // The member includes the instance id, so B cannot remove a connection of A
            (await storeB.RemoveConnectionAsync(userId, connectionId, ct)).Should().BeFalse();
            (await storeA.IsOnlineAsync(userId, ct)).Should().BeTrue();
        }

        [Fact]
        public async Task AddConnection_ManyAtTheSameTime_OnlyOneIsTheFirst()
        {
            var ct = TestContext.Current.CancellationToken;
            var userId = Guid.NewGuid();
            var connections = Enumerable.Range(0, 50).Select(_ => Guid.NewGuid()).ToList();

            var results = await Task.WhenAll(connections.Select((connectionId, i) =>
                (i % 2 == 0 ? storeA : storeB).AddConnectionAsync(userId, connectionId, ct)));

            results.Count(x => x).Should().Be(1);
            (await Db.SetLengthAsync(RedisPresenceStore.UserKey(userId))).Should().Be(50);
        }

        [Fact]
        public async Task RemoveConnection_ManyAtTheSameTime_OnlyOneIsTheLast()
        {
            var ct = TestContext.Current.CancellationToken;
            var userId = Guid.NewGuid();
            var connections = Enumerable.Range(0, 50).Select(_ => Guid.NewGuid()).ToList();
            for (var i = 0; i < connections.Count; i++)
            {
                await (i % 2 == 0 ? storeA : storeB).AddConnectionAsync(userId, connections[i], ct);
            }

            var results = await Task.WhenAll(connections.Select((connectionId, i) =>
                (i % 2 == 0 ? storeA : storeB).RemoveConnectionAsync(userId, connectionId, ct)));

            results.Count(x => x).Should().Be(1);
            (await storeA.IsOnlineAsync(userId, ct)).Should().BeFalse();
        }

        #endregion

        #region Online users

        [Fact]
        public async Task GetOnlineUsers_ReturnsOnlyTheOnlineOnes()
        {
            var ct = TestContext.Current.CancellationToken;
            var online1 = Guid.NewGuid();
            var online2 = Guid.NewGuid();
            var offline = Guid.NewGuid();
            var disconnected = Guid.NewGuid();
            var disconnectedConnection = Guid.NewGuid();

            await storeA.AddConnectionAsync(online1, Guid.NewGuid(), ct);
            await storeB.AddConnectionAsync(online2, Guid.NewGuid(), ct);
            await storeA.AddConnectionAsync(disconnected, disconnectedConnection, ct);
            await storeA.RemoveConnectionAsync(disconnected, disconnectedConnection, ct);

            var result = await storeA.GetOnlineUsersAsync([online1, offline, online2, disconnected], ct);

            result.Should().BeEquivalentTo(new[] { online1, online2 });
        }

        [Fact]
        public async Task GetOnlineUsers_EmptyList_ReturnsEmpty()
        {
            var ct = TestContext.Current.CancellationToken;

            (await storeA.GetOnlineUsersAsync([], ct)).Should().BeEmpty();
        }

        [Fact]
        public async Task GetOnlineUsers_DuplicatedIds_ReturnsEachOnce()
        {
            var ct = TestContext.Current.CancellationToken;
            var userId = Guid.NewGuid();
            await storeA.AddConnectionAsync(userId, Guid.NewGuid(), ct);

            (await storeA.GetOnlineUsersAsync([userId, userId, userId], ct)).Should().Equal(userId);
        }

        [Fact]
        public async Task IsOnline_NeverConnected_ReturnsFalse()
        {
            var ct = TestContext.Current.CancellationToken;

            (await storeA.IsOnlineAsync(Guid.NewGuid(), ct)).Should().BeFalse();
        }

        #endregion

        #region Instance and heartbeat

        [Fact]
        public async Task RegisterInstance_CreatesAliveKeyWithTtlAndAddsToInstances()
        {
            var ct = TestContext.Current.CancellationToken;

            await storeA.RegisterInstanceAsync(ct);

            var ttl = await Db.KeyTimeToLiveAsync(RedisPresenceStore.AliveKey(instanceA.Id));
            ttl.Should().NotBeNull();
            ttl!.Value.Should().BePositive().And.BeLessThanOrEqualTo(options.InstanceTtl);

            (await Db.SetContainsAsync(RedisPresenceStore.InstancesKey, instanceA.Id.ToString())).Should().BeTrue();
        }

        [Fact]
        public async Task Heartbeat_KeyExists_ReturnsTrueAndRenewsTtl()
        {
            var ct = TestContext.Current.CancellationToken;
            await storeA.RegisterInstanceAsync(ct);
            await Db.KeyExpireAsync(RedisPresenceStore.AliveKey(instanceA.Id), TimeSpan.FromSeconds(5));

            (await storeA.HeartbeatAsync(ct)).Should().BeTrue();

            var ttl = await Db.KeyTimeToLiveAsync(RedisPresenceStore.AliveKey(instanceA.Id));
            ttl!.Value.Should().BeGreaterThan(TimeSpan.FromSeconds(5));
        }

        [Fact]
        public async Task Heartbeat_KeyExpired_ReturnsFalseAndRegistersAgain()
        {
            var ct = TestContext.Current.CancellationToken;
            await storeA.RegisterInstanceAsync(ct);

            // Simulates a sweep of another instance after the key expired
            await Db.KeyDeleteAsync(RedisPresenceStore.AliveKey(instanceA.Id));
            await Db.SetRemoveAsync(RedisPresenceStore.InstancesKey, instanceA.Id.ToString());

            (await storeA.HeartbeatAsync(ct)).Should().BeFalse();

            (await Db.KeyExistsAsync(RedisPresenceStore.AliveKey(instanceA.Id))).Should().BeTrue();
            (await Db.SetContainsAsync(RedisPresenceStore.InstancesKey, instanceA.Id.ToString())).Should().BeTrue();

            // The next one finds the key again
            (await storeA.HeartbeatAsync(ct)).Should().BeTrue();
        }

        [Fact]
        public async Task AliveKey_ExpiresAfterTtl()
        {
            var ct = TestContext.Current.CancellationToken;
            var shortTtl = new RedisPresenceStore(redis, instanceA, new PresenceOptions { InstanceTtl = TimeSpan.FromSeconds(1) });

            await shortTtl.RegisterInstanceAsync(ct);
            (await Db.KeyExistsAsync(RedisPresenceStore.AliveKey(instanceA.Id))).Should().BeTrue();

            await Task.Delay(TimeSpan.FromMilliseconds(1500), ct);

            (await Db.KeyExistsAsync(RedisPresenceStore.AliveKey(instanceA.Id))).Should().BeFalse();
        }

        #endregion

        #region Sweep

        [Fact]
        public async Task Sweep_DeadInstance_RemovesItsConnectionsAndReturnsUsersNowOffline()
        {
            var ct = TestContext.Current.CancellationToken;
            await storeA.RegisterInstanceAsync(ct);
            await storeB.RegisterInstanceAsync(ct);

            var onlyInA = Guid.NewGuid();
            var inBoth = Guid.NewGuid();
            var onlyInB = Guid.NewGuid();
            await storeA.AddConnectionAsync(onlyInA, Guid.NewGuid(), ct);
            await storeA.AddConnectionAsync(onlyInA, Guid.NewGuid(), ct); // two tabs
            await storeA.AddConnectionAsync(inBoth, Guid.NewGuid(), ct);
            await storeB.AddConnectionAsync(inBoth, Guid.NewGuid(), ct);
            await storeB.AddConnectionAsync(onlyInB, Guid.NewGuid(), ct);

            // A dies: its key expires
            await Db.KeyDeleteAsync(RedisPresenceStore.AliveKey(instanceA.Id));

            var offline = await storeB.SweepDeadInstancesAsync(ct);

            offline.Should().BeEquivalentTo(new[] { onlyInA });
            (await storeB.IsOnlineAsync(onlyInA, ct)).Should().BeFalse();
            (await storeB.IsOnlineAsync(inBoth, ct)).Should().BeTrue();
            (await storeB.IsOnlineAsync(onlyInB, ct)).Should().BeTrue();
            (await Db.SetLengthAsync(RedisPresenceStore.UserKey(inBoth))).Should().Be(1);

            (await Db.KeyExistsAsync(RedisPresenceStore.InstanceConnectionsKey(instanceA.Id))).Should().BeFalse();
            (await Db.SetMembersAsync(RedisPresenceStore.InstancesKey)).Select(x => x.ToString())
                .Should().Equal(instanceB.Id.ToString());
        }

        [Fact]
        public async Task Sweep_AliveInstances_RemovesNothing()
        {
            var ct = TestContext.Current.CancellationToken;
            await storeA.RegisterInstanceAsync(ct);
            await storeB.RegisterInstanceAsync(ct);
            var userId = Guid.NewGuid();
            await storeA.AddConnectionAsync(userId, Guid.NewGuid(), ct);

            (await storeB.SweepDeadInstancesAsync(ct)).Should().BeEmpty();

            (await storeB.IsOnlineAsync(userId, ct)).Should().BeTrue();
            (await Db.SetLengthAsync(RedisPresenceStore.InstancesKey)).Should().Be(2);
        }

        [Fact]
        public async Task Sweep_NeverRemovesItsOwnInstance()
        {
            var ct = TestContext.Current.CancellationToken;
            await storeA.RegisterInstanceAsync(ct);
            var userId = Guid.NewGuid();
            await storeA.AddConnectionAsync(userId, Guid.NewGuid(), ct);

            // Its own key expired (long pause): the heartbeat resyncs, the sweep must not remove its connections
            await Db.KeyDeleteAsync(RedisPresenceStore.AliveKey(instanceA.Id));

            (await storeA.SweepDeadInstancesAsync(ct)).Should().BeEmpty();
            (await storeA.IsOnlineAsync(userId, ct)).Should().BeTrue();
        }

        [Fact]
        public async Task Sweep_TwoInstancesAtTheSameTime_EachUserReportedOnce()
        {
            var ct = TestContext.Current.CancellationToken;
            var deadInstance = new PresenceInstance();
            var deadStore = new RedisPresenceStore(redis, deadInstance, options);
            await deadStore.RegisterInstanceAsync(ct);
            await storeA.RegisterInstanceAsync(ct);
            await storeB.RegisterInstanceAsync(ct);

            var users = Enumerable.Range(0, 30).Select(_ => Guid.NewGuid()).ToList();
            foreach (var userId in users)
            {
                await deadStore.AddConnectionAsync(userId, Guid.NewGuid(), ct);
            }
            await Db.KeyDeleteAsync(RedisPresenceStore.AliveKey(deadInstance.Id));

            var results = await Task.WhenAll(storeA.SweepDeadInstancesAsync(ct), storeB.SweepDeadInstancesAsync(ct));

            results.SelectMany(x => x).Should().BeEquivalentTo(users);
            (await storeA.GetOnlineUsersAsync(users, ct)).Should().BeEmpty();
        }

        [Fact]
        public async Task Sweep_ConnectionAlreadyRemoved_NotReported()
        {
            var ct = TestContext.Current.CancellationToken;
            await storeA.RegisterInstanceAsync(ct);
            var userId = Guid.NewGuid();
            var connectionId = Guid.NewGuid();
            await storeA.AddConnectionAsync(userId, connectionId, ct);

            // Removed from the user set but still in the instance set (died between the two calls)
            await Db.SetRemoveAsync(RedisPresenceStore.UserKey(userId), RedisPresenceStore.UserMember(instanceA.Id, connectionId));
            await Db.KeyDeleteAsync(RedisPresenceStore.AliveKey(instanceA.Id));

            (await storeB.SweepDeadInstancesAsync(ct)).Should().BeEmpty();
            (await Db.KeyExistsAsync(RedisPresenceStore.InstanceConnectionsKey(instanceA.Id))).Should().BeFalse();
        }

        [Fact]
        public async Task Sweep_InvalidMembers_AreIgnored()
        {
            var ct = TestContext.Current.CancellationToken;
            await Db.SetAddAsync(RedisPresenceStore.InstancesKey, "foo");
            var deadInstance = Guid.NewGuid();
            await Db.SetAddAsync(RedisPresenceStore.InstancesKey, deadInstance.ToString());
            await Db.SetAddAsync(RedisPresenceStore.InstanceConnectionsKey(deadInstance), new RedisValue[] { "foo", "foo:bar", $"{Guid.NewGuid()}" });

            (await storeA.SweepDeadInstancesAsync(ct)).Should().BeEmpty();

            (await Db.KeyExistsAsync(RedisPresenceStore.InstanceConnectionsKey(deadInstance))).Should().BeFalse();
        }

        #endregion
    }
}
