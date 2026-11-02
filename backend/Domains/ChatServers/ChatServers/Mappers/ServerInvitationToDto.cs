using HootOut.ChatServers.Entities;
using HootOut.Contracts.ChatServers.Dtos;

namespace HootOut.ChatServers.Mappers
{
    public static class ChatServerServerInvitationToDto
    {
        public static ServerInvitationDto ToDto(this ServerInvitation invitation)
        {
            return new ServerInvitationDto
            {
                Id = invitation.Id,
                ChatServerId = invitation.ChatServerId,
                UserId = invitation.UserId,
                Status = invitation.Status,
                CreatedAt = invitation.CreatedAt,
                ModifiedAt = invitation.ModifiedAt,
            };
        }
    }
}
