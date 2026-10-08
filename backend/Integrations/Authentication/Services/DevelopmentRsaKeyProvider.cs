using HootOut.Contracts.Authentication.Services;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;

namespace HootOut.Authentication.Services
{
    public class DevelopmentRsaKeyProvider : IRsaKeyProvider
    {
        public RsaSecurityKey ValidationKey { get; private set; }
        public RsaSecurityKey SigningKey { get; private set; }

        public DevelopmentRsaKeyProvider(string privateKeyPath)
        {
            var fullPath = Path.GetFullPath(privateKeyPath);

            if (!File.Exists(fullPath))
                CreateKeyFile(fullPath);

            var rsa = RSA.Create();
            rsa.ImportFromPem(File.ReadAllText(fullPath));

            var publicBytes = rsa.ExportSubjectPublicKeyInfo();
            var keyId = Convert.ToHexString(SHA256.HashData(publicBytes))[..16];

            SigningKey = new RsaSecurityKey(rsa) { KeyId = keyId };

            var publicRsa = RSA.Create();
            publicRsa.ImportSubjectPublicKeyInfo(publicBytes, out _);
            ValidationKey = new RsaSecurityKey(publicRsa) { KeyId = keyId };
        }

        // Several hosts can start at once on a clean machine (parallel test fixtures, other processes).
        // Writing straight to the final path lets one of them read a half-written file, so write a
        // temporary file and publish it with a single move.
        private static void CreateKeyFile(string fullPath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

            var tempPath = $"{fullPath}.{Guid.NewGuid():N}.tmp";
            try
            {
                using (var stream = new FileStream(tempPath, new FileStreamOptions
                {
                    Mode = FileMode.CreateNew,
                    Access = FileAccess.Write,
                    UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite // owner only (ignored on Windows)
                }))
                using (var writer = new StreamWriter(stream))
                using (var newKey = RSA.Create(2048))
                {
                    writer.Write(newKey.ExportPkcs8PrivateKeyPem());
                }

                File.Move(tempPath, fullPath, overwrite: false);
            }
            catch (IOException) when (File.Exists(fullPath))
            {
                // Another host published its key first: use that one.
            }
            finally
            {
                File.Delete(tempPath); // nothing to delete if the move succeeded
            }
        }
    }
}
