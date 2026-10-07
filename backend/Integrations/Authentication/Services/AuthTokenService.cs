using HootOut.Authentication.Jwt;
using HootOut.Contracts.Authentication.Services;
using HootOut.Contracts.Users.Dtos;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;

namespace HootOut.Authentication.Services
{
    public class AuthTokenService : IAuthTokenService
    {
        private IRsaKeyProvider rsaKeyProvider;

        private JwtSettings jwtSettings;

        private readonly JsonWebTokenHandler handler = new();
        public int ExpiresInSeconds => jwtSettings.AccessTokenMinutes * 60;

        public AuthTokenService(
            IOptions<JwtSettings> options,
            IRsaKeyProvider rsaKeyProvider
            )
        {
            this.jwtSettings = options?.Value ?? throw new ArgumentNullException(nameof(options));
            this.rsaKeyProvider = rsaKeyProvider ?? throw new ArgumentNullException((nameof(rsaKeyProvider)));
        }

        public string CreateAccessToken(UserDto user)
        {
            var now = DateTime.UtcNow;

            var claims = new List<Claim>
            {
                new ("sub", user.Id.ToString()),
                new ("name", user.Username!),
                new ("email", user.Email!),
                new ("jti", Guid.NewGuid().ToString()),
                new("role", "user")
            };

            var descriptor = new SecurityTokenDescriptor
            {
                Issuer = jwtSettings.Issuer,
                Audience = jwtSettings.Audience,
                IssuedAt = now,
                NotBefore = now,
                Expires = now.AddMinutes(jwtSettings.AccessTokenMinutes),
                Subject = new ClaimsIdentity(claims),
                SigningCredentials = new SigningCredentials(rsaKeyProvider.SigningKey, SecurityAlgorithms.RsaSha256)
            };

            return handler.CreateToken(descriptor);
        }
    }
}
