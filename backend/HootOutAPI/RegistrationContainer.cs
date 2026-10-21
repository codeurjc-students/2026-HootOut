using Autofac;
using HootOut.HootOutAPI.Helpers;
using HootOut.Infraestructure.DI;

namespace HootOut.HootOutAPI
{
    public class RegistrationContainer : IRegistrationContainer
    {
        public void Register(ContainerBuilder builder)
        {
            builder.RegisterType<FileClassifier>().AsSelf().SingleInstance();
        }
    }
}
