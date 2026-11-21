using Dapper;
using HootOut.ChatChannels.Entities;
using HootOut.CommonDomain.Persistence;
using HootOut.Contracts.ChatConversations.Enums;
using HootOut.Contracts.Common.Saver;

namespace HootOut.PostgreSQL.Savers.ChatChannels
{
    public class ChatChannelsSaver : ISaver<ChatChannel>
    {
        private IPersistenceProvider provider;

        public ChatChannelsSaver(IPersistenceProvider provider)
        {
            this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        // The conversation is only inserted when the channel does not exist yet. On an update the
        // channel keeps its conversation (and its messages), and RETURNING gives back the one it has.
        private const string addOrUpdateChatChannel = """
            WITH conversation AS (
               INSERT INTO "HootOut"."ChatConversation" ("Id", "ConversationType", "CreatedAt", "ModifiedAt")
               SELECT @ChatConversationId, @ConversationType, @CreatedAt, @CreatedAt
               WHERE NOT EXISTS (SELECT 1 FROM "HootOut"."ChatChannel" WHERE "Id" = @Id)
               ON CONFLICT ("Id") DO NOTHING
            )
            INSERT INTO "HootOut"."ChatChannel" ("Id", "ChatServerId", "ChatConversationId", "Name", "CreatedAt", "ModifiedAt")
            VALUES (@Id, @ChatServerId, @ChatConversationId, @Name, @CreatedAt, @ModifiedAt)
            ON CONFLICT ("Id") DO UPDATE
                SET "Name" = @Name,
                    "ModifiedAt" = @ModifiedAt
            RETURNING "ChatConversationId"
        """;

        public async Task SaveAsync(ChatChannel item, CancellationToken ct = default)
        {
            await using var conn = await provider.OpenConnectionAsync(ct);
            await using var tx = await conn.BeginTransactionAsync(ct);

            // Only used if the channel is new
            if (item.ChatConversationId == Guid.Empty)
                item.ChatConversationId = Guid.CreateVersion7();

            try
            {
                item.ChatConversationId = await conn.ExecuteScalarAsync<Guid>(new CommandDefinition(
                    addOrUpdateChatChannel, new
                    {
                        item.Id,
                        item.ChatServerId,
                        item.ChatConversationId,
                        item.Name,
                        item.CreatedAt,
                        item.ModifiedAt,
                        ConversationType = ChatConversationType.Channel
                    }, tx, cancellationToken: ct));
                await tx.CommitAsync(ct);
            }
            catch (Exception ex)
            {
                var msg = $"Error while trying to save Chat Channel with uid {item.Id}";
                throw new Exception(msg, ex);
            }
        }

        public async Task DeleteAsync(ChatChannel item, CancellationToken ct = default)
        {
            await using var conn = await provider.OpenConnectionAsync(ct);
            await using var tx = await conn.BeginTransactionAsync(ct);

            try
            {
                await conn.ExecuteAsync(new CommandDefinition(
                    """ 
                    UPDATE "HootOut"."ChatChannel"
                    SET 
                        "Deleted" = True,
                        "ModifiedAt" = @ModifiedAt
                    WHERE "Id" = @Id
                    """, new

                    {
                        item.Id,
                        ModifiedAt = DateTime.UtcNow
                    }, tx, cancellationToken: ct));
                await tx.CommitAsync(ct);
            }
            catch (Exception ex)
            {
                var msg = $"Error while trying to delete Chat Channel with uid {item.Id}";
                throw new Exception(msg, ex);
            }
        }
    }
}
