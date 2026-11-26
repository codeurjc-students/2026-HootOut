using HootOut.Contracts.Common.Dto;
using HootOut.Contracts.Users.Dtos;
using HootOut.Contracts.Users.Dtos.Request;

namespace HootOut.Contracts.Users.Services
{
    public interface IUserService
    {
        Task<UserDto> CreateUserAsync(CreateUserRequest createUserRequest, CancellationToken ct = default);
        Task<UserDto?> GetUserDtoByIdAsync(Guid userId, CancellationToken ct = default);
        Task<UserDto?> GetUserDtoByUsernameAsync(string username, CancellationToken ct = default);
        Task<PictureDto> UpdateProfilePictureAsync(Guid mediaFileId, Guid userId, CancellationToken ct);
    }
}
