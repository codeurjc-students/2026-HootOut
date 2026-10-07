using HootOut.Users.Entities;

namespace HootOut.Users.Search
{
    public interface IUserSearch
    {
        IEnumerable<UserInfo> GetAllUsers();
        UserInfo? GetUserByEmail(string email);

        UserInfo? GetUserById(Guid id);
    }
}
