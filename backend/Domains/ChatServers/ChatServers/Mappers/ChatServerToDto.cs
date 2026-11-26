using HootOut.ChatServers.Entities;
using HootOut.Contracts.ChatServers.Dtos;
using HootOut.Contracts.Common.Dto;

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
                ProfilePicture = chatServer.ProfilePictureId == null ? null : new PictureDto { MediaFileId = chatServer.ProfilePictureId.Value },
                CreatedAt = chatServer.CreatedAt,
                ModifiedAt = chatServer.ModifiedAt,
            };
        }
    }
}
