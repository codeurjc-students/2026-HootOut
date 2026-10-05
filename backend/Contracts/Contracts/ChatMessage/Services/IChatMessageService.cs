using HootOut.Contracts.ChatMessage.Dtos;
using HootOut.Contracts.ChatMessage.Requests;

namespace HootOut.Contracts.ChatMessage.Services
{
    public interface IChatMessageService
    {
        IEnumerable<ChatMessageDto> GetMessagesByChannel(Guid channelId);
        ChatMessageDto CreateChatMessage(CreateChatMessageRequest request);
    }
}
