using HootOut.Contracts.Users.Dtos;
using HootOut.Users.Entities;

namespace HootOut.Users.Mappers
{
    public static class UserToDto
    {
        public static UserDto ToDto(this UserInfo user)
        {
            return new UserDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                Password = user.Password
            };
        }
    }
}
