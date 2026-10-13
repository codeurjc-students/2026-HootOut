using Dapper;
using HootOut.ChatChannels.ChatChannels;
using HootOut.CommonDomain.Persistence;
using HootOut.Contracts.Common.Saver;

namespace HootOut.PostgreSQL.Savers.ChatChannels
{
    internal class ChatChannelsSaver : ISaver<ChatChannel>
    {
        private IPersistenceProvider provider;

        public ChatChannelsSaver(IPersistenceProvider provider)
        {
            this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        private const string addOrReplaceChatChannel = """
            INSERT INTO "HootOut"."ChatChannel" ("Id", "ChatServerId", "Name", "CreatedAt", "ModifiedAt") 
            VALUES (@Id, @ChatServerId, @Name, @CreatedAt, @ModifiedAt)
        """;

        public async Task SaveAsync(ChatChannel item, CancellationToken ct = default)
        {
            await using var conn = await provider.OpenConnectionAsync(ct);
            await using var tx = await conn.BeginTransactionAsync(ct);

            try
            {
                await conn.ExecuteAsync(new CommandDefinition(
                    addOrReplaceChatChannel, new
                    {
                        item.Id,
                        item.ChatServerId,
                        item.Name,
                        item.CreatedAt,
                        item.ModifiedAt
                    }, tx, cancellationToken: ct));
                await tx.CommitAsync(ct);
            }
            catch (Exception ex)
            {
                var msg = $"Error while trying to save Chat Channel with uid {item.Id}";
                throw new Exception(msg, ex);
            }
        }
    }
}
