using HootOut.Contracts.Users.Dtos;
using HootOut.Users.Entities;

namespace HootOut.Users.Repositories
{
    public interface IUserRepository
    {
        Task<IEnumerable<UserInfo>> GetAllUsersAsync(CancellationToken ct = default);
        Task<UserInfo?> GetUserByEmailAsync(string email, CancellationToken ct = default);

        Task<UserInfo?> GetUserByIdAsync(Guid id, CancellationToken ct = default);

        Task<UserInfo?> GetUserByUsernameAsync(string username, CancellationToken ct = default);
        Task<UserDto?> GetUserDtoByIdAsync(Guid id, CancellationToken ct);
        Task UpdateProfilePictureAsync(Guid mediaFileId, Guid userId, CancellationToken ct);
    }
}
