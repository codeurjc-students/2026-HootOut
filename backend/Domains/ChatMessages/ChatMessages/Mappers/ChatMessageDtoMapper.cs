using HootOut.ChatMessages.ChatMessages;
using HootOut.Contracts.ChatMessage.Dtos;

namespace HootOut.ChatMessages.Mappers
{
    public static class ChatMessageDtoMapper
    {
        public static ChatMessageDto ToDto(this ChatMessage chatMessage)
        {
            return new ChatMessageDto
            {
                Id = chatMessage.Id,
                Content = chatMessage.Content,
                ChatChannelId = chatMessage.ChatChannelId,
                Author = new Contracts.Users.Dtos.UserDto
                {
                    Id = chatMessage.AuthorId
                },
                CreatedAt = chatMessage.CreatedAt,
                ModifiedAt = chatMessage.ModifiedAt,
            };
        }
    }
}
