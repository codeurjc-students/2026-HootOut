using HootOut.Users.Entities;

namespace HootOut.Users.Search
{
    public interface IUserSearch
    {
        Task<IEnumerable<UserInfo>> GetAllUsersAsync(CancellationToken ct = default);
        Task<UserInfo?> GetUserByEmailAsync(string email, CancellationToken ct = default);

        Task<UserInfo?> GetUserByIdAsync(Guid id, CancellationToken ct = default);
    }
}
