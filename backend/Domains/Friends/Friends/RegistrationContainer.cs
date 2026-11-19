using Autofac;
using HootOut.Contracts.Friends.Services;
using HootOut.Friends.Services;
using HootOut.Infraestructure.DI;

namespace HootOut.Friends
{
    public class RegistrationContainer : IRegistrationContainer
    {
        public void Register(ContainerBuilder builder)
        {
            builder.RegisterType<FriendsService>().As<IFriendsService>().SingleInstance();
        }
    }
}
