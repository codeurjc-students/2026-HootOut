using HootOut.Contracts.Authentication.Services;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;

namespace HootOut.Authentication.Services
{
    public class RsaKeyProvider : IRsaKeyProvider
    {
        public RsaSecurityKey ValidationKey { get; private set; }
        public RsaSecurityKey SigningKey { get; private set; }

        public RsaKeyProvider(string privateKeyPath)
        {
            var rsa = RSA.Create(2048);

            if (File.Exists(privateKeyPath))
            {
                rsa.ImportFromPem(File.ReadAllText(privateKeyPath));
            }
            else
            {
                var fullPath = Path.GetFullPath(privateKeyPath);
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

                using var writer = new StreamWriter(fullPath, new FileStreamOptions
                {
                    Mode = FileMode.CreateNew,
                    Access = FileAccess.Write
                });
                writer.Write(rsa.ExportPkcs8PrivateKeyPem());
            }

            var publicBytes = rsa.ExportSubjectPublicKeyInfo();
            var keyId = Convert.ToHexString(SHA256.HashData(publicBytes))[..16];

            SigningKey = new RsaSecurityKey(rsa) { KeyId = keyId };

            var publicRsa = RSA.Create();
            publicRsa.ImportSubjectPublicKeyInfo(publicBytes, out _);
            ValidationKey = new RsaSecurityKey(publicRsa) { KeyId = keyId };
        }
    }
}
