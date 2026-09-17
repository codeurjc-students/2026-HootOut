using HootOut.Contracts.Users.Dtos;

namespace HootOut.Contracts.Users.Search
{
    public interface IUserSearch
    {
        IEnumerable<UserDto> GetUserDtos();
    }
}
