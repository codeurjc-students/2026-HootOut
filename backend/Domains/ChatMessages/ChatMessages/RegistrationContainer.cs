using Autofac;
using HootOut.ChatMessages.Services;
using HootOut.Contracts.ChatMessage.Services;
using HootOut.Infraestructure.DI;

namespace HootOut.ChatMessages
{
    public class RegistrationContainer : IRegistrationContainer
    {
        public void Register(ContainerBuilder builder)
        {
            builder.RegisterType<ChatMessageService>().As<IChatMessageService>().SingleInstance();
        }
    }
}
