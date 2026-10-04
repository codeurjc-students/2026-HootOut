using Dapper;
using HootOut.ChatChannels.ChatChannels;
using HootOut.CommonDomain.Persistence;
using HootOut.Contracts.Common.Saver;
using HootOut.Users.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace HootOut.PostgreSQL.Savers.ChatChannels
{
    internal class ChatChannelsSaver: ISaver<ChatChannel>
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

        public ChatChannel Save(ChatChannel item)
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
                        CreatedAt = now,
                        ModifiedAt = now
                    }, transaction);
                    transaction.Commit();

                    return item;
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
