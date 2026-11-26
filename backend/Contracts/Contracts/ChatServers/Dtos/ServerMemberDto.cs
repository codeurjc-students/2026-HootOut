using HootOut.Contracts.Common.Dto;

namespace HootOut.Contracts.ChatServers.Dtos
{
    public class ServerMemberDto
    {
        public Guid Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public Guid UserId { get; set; }
        public PictureDto? ProfilePicture { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}