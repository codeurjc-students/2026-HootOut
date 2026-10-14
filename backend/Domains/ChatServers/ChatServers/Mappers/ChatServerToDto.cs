using HootOut.ChatServers.ChatServers;
using HootOut.Contracts.ChatServers.Dtos;

namespace HootOut.ChatServers.Mappers
{
    public static class ChatServerToDto
    {
        public static ChatServerDto ToDto(this ChatServer chatServer)
        {
            return new ChatServerDto
            {
                Id = chatServer.Id,
                Name = chatServer.Name,
                Description = chatServer.Description,
                OwnerId = chatServer.OwnerId,
                Channels = [],
                CreatedAt = chatServer.CreatedAt,
                ModifiedAt = chatServer.ModifiedAt,
            };
        }
    }
}
