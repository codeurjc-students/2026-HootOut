using HootOut.Contracts.Users.Dtos;
using HootOut.Contracts.Users.Dtos.Request;

namespace HootOut.Contracts.Users.Services
{
    public interface IUserService
    {
        Guid CreateUser(CreateUserRequest createUserRequest); 
        IEnumerable<UserDto> GetUserDtos();
    }
}
