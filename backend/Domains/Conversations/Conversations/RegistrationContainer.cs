using Autofac;
using HootOut.ChatConversations.Services;
using HootOut.Contracts.ChatConversations.Services;
using HootOut.Infraestructure.DI;

namespace HootOut.ChatConversations
{
    public class RegistrationContainer : IRegistrationContainer
    {
        public void Register(ContainerBuilder builder)
        {
            builder.RegisterType<ChatConversationsService>().As<IChatConversationsService>().SingleInstance();
            builder.RegisterType<DirectConversationsService>().As<IDirectConversationsService>().SingleInstance();
        }
    }
}
