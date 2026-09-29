using Autofac;
using HootOut.Contracts.WebSockets.Services;
using HootOut.HootOutWebsockets.Services;
using HootOut.Infraestructure.DI;
using HootOut.WebSockets.Service;

namespace HootOut.HootOutWebsockets
{
    public class RegistrationContainer : IRegistrationContainer
    {
        public void Register(ContainerBuilder builder)
        {
            builder.RegisterType<ConnectionManager>().As<IWSConnectionManager>().SingleInstance();
            builder.RegisterType<WSPubSubService>().As<IWSPubSubService>().SingleInstance();
        }
    }
}
