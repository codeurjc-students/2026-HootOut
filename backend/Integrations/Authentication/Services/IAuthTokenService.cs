using HootOut.Contracts.Users.Dtos;

namespace HootOut.Authentication.Services
{
    public interface IAuthTokenService
    {
        string CreateAccessToken(UserDto user);
        int ExpiresInSeconds { get; }
    }
}
