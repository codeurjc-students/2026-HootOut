using HootOut.Contracts.ChatMessage.Dtos;
using HootOut.Contracts.Common;

namespace HootOut.ChatMessages.Repositories
{
    public interface IChatMessageRepository
    {
        Task<PageResult<ChatMessageDto>> SearchMessagesByChannelIdAsync(Guid channelId, Guid? next = null, int pageSize = 10, CancellationToken ct = default);
    }
}
