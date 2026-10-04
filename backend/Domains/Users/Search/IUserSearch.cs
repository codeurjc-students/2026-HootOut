using HootOut.Contracts.Users.Dtos;
using HootOut.Users.Entities;

namespace HootOut.Contracts.Users.Search
{
    public interface IUserSearch
    {
        IEnumerable<UserInfo> GetAllUsers();
    }
}
