using Autofac;
using HootOut.ChatServers.Services;
using HootOut.Contracts.ChatServers.Services;
using HootOut.Infraestructure.DI;

namespace HootOut.ChatServers
{
    public sealed class RegistrationContianer : IRegistrationContainer
    {
        public void Register(ContainerBuilder builder)
        {
            builder.RegisterType<ChatServerService>().As<IChatServerService>().SingleInstance();
        }
    }
}
