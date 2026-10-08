namespace HootOut.Authentication.Jwt
{
    public sealed class JwtSettings
    {
        public string Issuer { get; init; } = string.Empty;
        public string Audience { get; init; } = string.Empty;
        public int AccessTokenMinutes { get; init; } = 15;
        public int RefreshTokenDays { get; init; } = 14;

        public string PrivateKeysPath { get; init; } = "keys/jwt-private.pem"; //TO-DO For development only
    }
}
