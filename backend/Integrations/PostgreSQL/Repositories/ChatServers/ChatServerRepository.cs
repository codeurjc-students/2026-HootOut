using Dapper;
using HootOut.ChatServers.Entities;
using HootOut.ChatServers.Repositories;
using HootOut.CommonDomain.Persistence;
using HootOut.Contracts.ChatChannels.Dtos;
using HootOut.Contracts.ChatMessage.Dtos;
using HootOut.Contracts.ChatServers.Dtos;

namespace HootOut.PostgreSQL.Repositories.ChatServers
{
    public sealed class ChatServerRepository : IChatServerRepository
    {
        private IPersistenceProvider provider;

        public ChatServerRepository(IPersistenceProvider provider)
        {
            this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }
        private const string serverColumns =
            """ server."Id", server."Name", server."Description", server."OwnerId", server."CreatedAt", server."ModifiedAt" """;

        public async Task<IEnumerable<ChatServer>> GetServersByUserAsync(Guid userId, CancellationToken ct = default)
        {
            await using var conn = await provider.OpenConnectionAsync(ct);

            return await conn.QueryAsync<ChatServer>(new CommandDefinition($"""
                SELECT 
                    {serverColumns}
                FROM "HootOut"."ChatServer" server
                INNER JOIN "HootOut"."ServerMember" member
                    ON server."Id" = member."ChatServerId"
                WHERE member."UserId" = @UserId 
                    AND server."Deleted" = FALSE AND member."Deleted" = FALSE
                ORDER BY server."CreatedAt" DESC
                """,
                new { UserId = userId },
                cancellationToken: ct));
        }

        public async Task<ChatServerDto?> GetServerDtoByIdAsync(Guid serverId, CancellationToken ct)
        {
            await using var conn = await provider.OpenConnectionAsync(ct);
            var sql = $"""
                SELECT 
                    {serverColumns},
                    channel."Id", 
                    channel."ChatServerId",
                    channel."Name"
                FROM "HootOut"."ChatServer" server
                LEFT JOIN "HootOut"."ChatChannel" channel
                    ON server."Id" = channel."ChatServerId" AND channel."Deleted" = FALSE
                WHERE server."Id" = @ServerId AND server."Deleted" = FALSE 
                """;

            ChatServerDto? result = null;

            await conn.QueryAsync<ChatServerDto?, ChatChannelDto?, ChatServerDto?>(new CommandDefinition(sql, new
            {
                ServerId = serverId
            }, cancellationToken: ct),
            (server, channel) =>
            {
                if (server == null)
                    return null;

                if (result == null)
                {
                    result = server;
                }

                if (channel != null)
                    result.Channels.Add(channel);
                return server;
            }, splitOn: "Id");

            return result;
        }
    }
}
