using Autofac;
using HootOut.ChatChannels.Services;
using HootOut.Contracts.ChatChannels.Services;
using HootOut.Infraestructure.DI;

namespace HootOut.ChatChannels
{
    public class RegistrationContainer : IRegistrationContainer
    {
        public void Register(ContainerBuilder builder)
        {
            builder.RegisterType<ChatChannelsService>().As<IChatChannelsService>();
        }
    }
}
