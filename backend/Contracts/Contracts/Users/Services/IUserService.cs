using HootOut.Contracts.Users.Dtos;
using HootOut.Contracts.Users.Dtos.Request;

namespace HootOut.Contracts.Users.Services
{
    public interface IUserService
    {
        UserDto CreateUser(CreateUserRequest createUserRequest);
        UserDto? GetUserDtoById(Guid userId);
    }
}
