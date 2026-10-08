using Microsoft.IdentityModel.Tokens;

namespace HootOut.Contracts.Authentication.Services
{
    public interface IRsaKeyProvider
    {
        RsaSecurityKey SigningKey { get; }

        RsaSecurityKey ValidationKey { get; }
    }
}
