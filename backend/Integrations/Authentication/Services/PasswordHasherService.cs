using HootOut.Contracts.Authentication.Services;
using System.Security.Cryptography;

namespace HootOut.Authentication.Services
{
    public class PasswordHasherService : IPasswordHasherService
    {
        private const int SaltSize = 16;          // 128-bit salt
        private const int HashSize = 32;          // 256-bit hash
        private const int Iterations = 600_000;   // OWASP guidance for PBKDF2-HMAC-SHA256

        public string HashPassword(string password)
        {
            var salt = RandomNumberGenerator.GetBytes(SaltSize);
            var hash = Rfc2898DeriveBytes.Pbkdf2(
                password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);

            return $"v1.{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
        }

        public bool VerifyPassword(string password, string hashedPassword)
        {
            var parts = hashedPassword.Split('.');
            if (parts.Length != 4 || parts[0] != "v1" || !int.TryParse(parts[1], out var iterations))
                return false;

            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);

            var actual = Rfc2898DeriveBytes.Pbkdf2(
            password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);

            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
    }
}
