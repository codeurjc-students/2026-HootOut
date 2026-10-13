using HootOut.ChatServers.ChatServers;
using System;
using System.Collections.Generic;
using System.Text;

namespace HootOut.ChatServers.Searchs
{
    public interface IChatServerSearch
    {
        Task<IEnumerable<ChatServer>> GetAllChatServersAsync(CancellationToken ct = default);
    }
}
