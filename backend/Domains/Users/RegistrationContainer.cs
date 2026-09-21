using Autofac;
using HootOut.Contracts.Users.Services;
using HootOut.Infraestructure.DI;
using HootOut.Users.Services;

namespace HootOut.Users
{
    public class RegistrationContainer : IRegistrationContainer
    {
        public void Register(ContainerBuilder builder)
        {
            builder.RegisterType<UserService>().As<IUserService>().SingleInstance();
        }
    }
}
