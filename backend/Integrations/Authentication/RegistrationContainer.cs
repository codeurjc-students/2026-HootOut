using Autofac;
using HootOut.Authentication.Services;
using HootOut.Contracts.Authentication.Services;
using HootOut.Infraestructure.DI;

namespace HootOut.Authentication
{
    public class RegistrationContainer : IRegistrationContainer
    {
        public void Register(ContainerBuilder builder)
        {
            builder.RegisterType<AuthenticationService>().As<IAuthenticationService>().SingleInstance();
            builder.RegisterType<AuthTokenService>().As<IAuthTokenService>().SingleInstance();
            builder.RegisterType<RefreshTokenService>().As<IRefreshTokenService>().SingleInstance();
            builder.RegisterType<PasswordHasherService>().As<IPasswordHasherService>().SingleInstance();
        }
    }
}
