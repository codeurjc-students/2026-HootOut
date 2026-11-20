using Dapper;
using HootOut.ChatChannels.Entities;
using HootOut.ChatConversations.Entities;
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

        private const string addOrUpdateChatChannel = """
            WITH conversation AS (
               INSERT INTO "HootOut"."ChatConversation" ("Id", "ConversationType", "CreatedAt", "ModifiedAt")
               VALUES (@ChatConversationId, @ConversationType, @Now, @Now)
               ON CONFLICT ("Id") DO NOTHING
           )

            INSERT INTO "HootOut"."ChatChannel" ("Id", "ChatServerId", "ChatConversationId", "Name", "CreatedAt", "ModifiedAt") 
            VALUES (@Id, @ChatServerId, @ChatConversationId, @Name, @CreatedAt, @ModifiedAt)
            ON CONFLICT ("Id") DO UPDATE
                SET "Name" = @Name,
                    "ModifiedAt" = @ModifiedAt
        """;

        public async Task SaveAsync(ChatChannel item, CancellationToken ct = default)
        {
            await using var conn = await provider.OpenConnectionAsync(ct);
            await using var tx = await conn.BeginTransactionAsync(ct);

            var conversation = Persistable.CreateNew<ChatConversation>();
            conversation.ConversationType = ChatConversationType.Channel;
            item.ChatConversationid = conversation.Id;
            try
            {
                await conn.ExecuteAsync(new CommandDefinition(
                    addOrUpdateChatChannel, new
                    {
                        item.Id,
                        item.ChatServerId,
                        item.Name,
                        item.CreatedAt,
                        item.ModifiedAt,
                        ChatConversationId = conversation.Id,
                        ConversationType = conversation.ConversationType,
                        Now = DateTime.UtcNow
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
