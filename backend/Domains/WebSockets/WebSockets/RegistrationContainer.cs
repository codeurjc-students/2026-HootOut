using Autofac;
using HootOut.Contracts.WebSockets.Handlers;
using HootOut.Contracts.WebSockets.Services;
using HootOut.Infraestructure.DI;
using HootOut.WebSockets.Actions;
using HootOut.WebSockets.Handlers;
using HootOut.WebSockets.Service;

namespace HootOut.WebSockets
{
    public class RegistrationContainer : IRegistrationContainer
    {
        public void Register(ContainerBuilder builder)
        {
            builder.RegisterType<WSMessageDeserializer>().As<IWSMessageDeserializer>().SingleInstance();

            // Register Message Handlers
            builder.RegisterType<ChannelSubscribeHandler>().As<IWSMessageHandler>().SingleInstance();
            builder.RegisterType<ChannelUnsubscribeHandler>().As<IWSMessageHandler>().SingleInstance();
            builder.RegisterType<ChatMessageHandler>().As<IWSMessageHandler>().SingleInstance();
        }
    }
}
