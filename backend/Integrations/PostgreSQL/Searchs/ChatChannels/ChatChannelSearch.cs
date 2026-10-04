using Dapper;
using HootOut.ChatChannels.ChatChannels;
using HootOut.ChatChannels.Search;
using HootOut.CommonDomain.Persistence;
using HootOut.Contracts.ChatChannels.Dtos; 
using HootOut.Contracts.Users.Dtos;
using System;
using System.Collections.Generic;
using System.Text;

namespace HootOut.PostgreSQL.Searchs.ChatChannels
{
    public class ChatChannelSearch : IChatChannelSearch
    {
        private IPersistenceProvider persistenceProvider;
        public ChatChannelSearch(IPersistenceProvider persistenceProvider)
        {
            this.persistenceProvider = persistenceProvider ?? throw new ArgumentNullException(nameof(persistenceProvider));
        }

        private const string getAllChannelsQuery = """
            SELECT 
                "Id",
                "Name",
                "CreatedAt",
                "ModifiedAt"
            FROM "HootOut"."ChatChannel"
        """;

        public IEnumerable<ChatChannel> getAllChannels()
        {
            using(var conn = persistenceProvider.GetNewConnection())
            {
                return conn.Query<ChatChannel>(getAllChannelsQuery);
            }
        }
    }
}
