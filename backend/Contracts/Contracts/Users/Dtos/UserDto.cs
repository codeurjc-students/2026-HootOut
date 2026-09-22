namespace HootOut.Contracts.Users.Dtos
{
    public class UserDto
    {
        public required Guid Uid { get; set; }

        public required string Username { get; set; }

        public required string Email { get; set; }

        public required string Password { get; set; }
    }
}
