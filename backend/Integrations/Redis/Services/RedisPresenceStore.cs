using HootOut.Contracts.Common.Presence;
using StackExchange.Redis;

namespace HootOut.Redis.Services
{
    /// <summary>
    /// Keys:
    /// <list type="bullet">
    /// <item><c>{presence}:user:&lt;userId&gt;</c> SET of "&lt;instanceId&gt;:&lt;connectionId&gt;", one per tab/device.</item>
    /// <item><c>{presence}:online</c> SET of the ids of the users with at least one connection.</item>
    /// <item><c>presence:instance:&lt;instanceId&gt;:alive</c> string with TTL, renewed by the heartbeat.</item>
    /// <item><c>presence:instance:&lt;instanceId&gt;:connections</c> SET of "&lt;userId&gt;:&lt;connectionId&gt;", used by the sweep.</item>
    /// <item><c>presence:instances</c> SET of instance ids, so the sweep does not need SCAN.</item>
    /// </list>
    /// The scripts change the key of the user and <c>{presence}:online</c> in one atomic step. In cluster mode
    /// both keys must be in the same slot: "{presence}" is a hash tag, so every user key and the online set go
    /// to the same slot. The instance keys are only used one at a time and keep their own slots.
    /// </summary>
    public sealed class RedisPresenceStore : IPresenceStore
    {
        // SADD + SCARD in one atomic step: two instances adding the first two connections of a user
        // at the same time cannot both see "first connection".
        // The user is always added to the online set (SADD is idempotent), so the set repairs itself
        // if it ever missed the user (e.g. it was created after the user connected).
        public const string AddScript = """
            local added = redis.call('SADD', KEYS[1], ARGV[1])
            redis.call('SADD', KEYS[2], ARGV[2])
            if added == 1 and redis.call('SCARD', KEYS[1]) == 1 then
                return 1
            end
            return 0
            """;

        // SREM returns 0 if the member was already removed (e.g. by a sweep running in another instance),
        // so the online -> offline transition is reported exactly once.
        // The user leaves the online set whenever it has no connections left, also when the transition was
        // already reported, so the set never keeps a user without connections.
        public const string RemoveScript = """
            local removed = redis.call('SREM', KEYS[1], ARGV[1])
            if redis.call('SCARD', KEYS[1]) == 0 then
                redis.call('SREM', KEYS[2], ARGV[2])
                if removed == 1 then
                    return 1
                end
            end
            return 0
            """;

        public const string InstancesKey = "presence:instances";
        public static readonly RedisKey OnlineKey = "{presence}:online";

        private const int MembershipChunkSize = 1000;

        private readonly IConnectionMultiplexer redis;
        private readonly PresenceInstance instance;
        private readonly PresenceOptions options;

        public RedisPresenceStore(IConnectionMultiplexer redis, PresenceInstance instance, PresenceOptions options)
        {
            this.redis = redis ?? throw new ArgumentNullException(nameof(redis));
            this.instance = instance ?? throw new ArgumentNullException(nameof(instance));
            this.options = options ?? throw new ArgumentNullException(nameof(options));
        }

        private IDatabase Db => redis.GetDatabase();

        public async Task<bool> AddConnectionAsync(Guid userId, Guid connectionId, CancellationToken ct = default)
        {
            // Instance index first: if the process dies between the two calls the sweep still finds the connection
            await Db.SetAddAsync(InstanceConnectionsKey(instance.Id), InstanceMember(userId, connectionId));

            var result = await Db.ScriptEvaluateAsync(AddScript,
                [UserKey(userId), OnlineKey],
                [UserMember(instance.Id, connectionId), userId.ToString()]);

            return (int)result == 1;
        }

        public async Task<bool> RemoveConnectionAsync(Guid userId, Guid connectionId, CancellationToken ct = default)
        {
            var result = await Db.ScriptEvaluateAsync(RemoveScript,
                [UserKey(userId), OnlineKey],
                [UserMember(instance.Id, connectionId), userId.ToString()]);

            await Db.SetRemoveAsync(InstanceConnectionsKey(instance.Id), InstanceMember(userId, connectionId));

            return (int)result == 1;
        }

        public async Task<bool> IsOnlineAsync(Guid userId, CancellationToken ct = default)
        {
            return await Db.SetContainsAsync(OnlineKey, userId.ToString());
        }

        public async Task<IReadOnlySet<Guid>> GetOnlineUsersAsync(IEnumerable<Guid> userIds, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(userIds);

            var ids = userIds.Distinct().ToArray();
            if (ids.Length == 0)
                return new HashSet<Guid>();

            var db = Db;
            var online = new HashSet<Guid>();

            // SMISMEMBER: one command for a whole chunk instead of one SCARD per user.
            // Chunks keep each command small for servers with many members
            foreach (var chunk in ids.Chunk(MembershipChunkSize))
            {
                ct.ThrowIfCancellationRequested();

                var values = chunk.Select(id => (RedisValue)id.ToString()).ToArray();
                var contained = await db.SetContainsAsync(OnlineKey, values);
                for (var i = 0; i < chunk.Length; i++)
                {
                    if (contained[i])
                        online.Add(chunk[i]);
                }
            }

            return online;
        }

        public async Task RegisterInstanceAsync(CancellationToken ct = default)
        {
            await Db.StringSetAsync(AliveKey(instance.Id), DateTime.UtcNow.ToString("O"), options.InstanceTtl);
            await Db.SetAddAsync(InstancesKey, instance.Id.ToString());
        }

        public async Task<bool> HeartbeatAsync(CancellationToken ct = default)
        {
            // Only renew if it still exists. If it does not, the instance was considered dead.
            var renewed = await Db.StringSetAsync(AliveKey(instance.Id), DateTime.UtcNow.ToString("O"), options.InstanceTtl, When.Exists);
            if (!renewed)
            {
                await RegisterInstanceAsync(ct);
            }
            return renewed;
        }

        public async Task<IReadOnlyCollection<Guid>> SweepDeadInstancesAsync(CancellationToken ct = default)
        {
            var db = Db;
            var offlineUsers = new HashSet<Guid>();

            foreach (var value in await db.SetMembersAsync(InstancesKey))
            {
                ct.ThrowIfCancellationRequested();

                if (!Guid.TryParse(value.ToString(), out var instanceId) || instanceId == instance.Id)
                    continue;

                if (await db.KeyExistsAsync(AliveKey(instanceId)))
                    continue;

                foreach (var member in await db.SetMembersAsync(InstanceConnectionsKey(instanceId)))
                {
                    if (!TryParseInstanceMember(member.ToString(), out var userId, out var connectionId))
                        continue;

                    // Several instances can sweep the same dead instance at the same time: the script
                    // only reports the transition to the one whose SREM removed the last member
                    var result = await db.ScriptEvaluateAsync(RemoveScript,
                        [UserKey(userId), OnlineKey],
                        [UserMember(instanceId, connectionId), userId.ToString()]);

                    if ((int)result == 1)
                        offlineUsers.Add(userId);
                }

                await db.KeyDeleteAsync(InstanceConnectionsKey(instanceId));
                await db.SetRemoveAsync(InstancesKey, instanceId.ToString());
            }

            return offlineUsers;
        }

        public static RedisKey UserKey(Guid userId) => $"{{presence}}:user:{userId}";
        public static RedisKey AliveKey(Guid instanceId) => $"presence:instance:{instanceId}:alive";
        public static RedisKey InstanceConnectionsKey(Guid instanceId) => $"presence:instance:{instanceId}:connections";
        public static RedisValue UserMember(Guid instanceId, Guid connectionId) => $"{instanceId}:{connectionId}";
        public static RedisValue InstanceMember(Guid userId, Guid connectionId) => $"{userId}:{connectionId}";

        private static bool TryParseInstanceMember(string member, out Guid userId, out Guid connectionId)
        {
            userId = Guid.Empty;
            connectionId = Guid.Empty;
            var parts = member.Split(':');
            return parts.Length == 2
                && Guid.TryParse(parts[0], out userId)
                && Guid.TryParse(parts[1], out connectionId);
        }
    }
}
