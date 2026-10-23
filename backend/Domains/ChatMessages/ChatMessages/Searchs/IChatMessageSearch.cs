using HootOut.CommonDomain.Persistence;
using HootOut.Contracts.ChatMessage.Dtos;
using HootOut.Contracts.Common;

namespace HootOut.ChatMessages.Searchs
{
    public interface IChatMessageSearch
    {
        Task<PageResult<ChatMessageDto>> SearchMessagesByChannelIdAsync(Guid channelId, Guid? next = null, int pageSize = 10, CancellationToken ct = default);
    }
}
