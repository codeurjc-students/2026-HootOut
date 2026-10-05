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

        public void Save(ChatChannel item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));

            try
            {
                using (var conn = provider.GetNewConnection())
                {
                    var transaction = conn.BeginTransaction();

                    var now = DateTime.UtcNow;

                    conn.Execute(addOrReplaceChatChannel, new
                    {
                        item.Id,
                        item.ChatServerId,
                        item.Name,
                        item.CreatedAt,
                        item.ModifiedAt
                    }, transaction);
                    transaction.Commit();
                }
            }
            catch (Exception ex)
            {
                var msg = $"Error while trying to save Chat Channel with uid {item.Id}";
                throw new Exception(msg, ex);
            }
        }
    }
}
