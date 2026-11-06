using Autofac;
using HootOut.CommonDomain.DefaultValues;
using HootOut.HootOutAPI.Helpers;
using HootOut.HootOutAPI.Init;
using HootOut.Infraestructure.DI;

namespace HootOut.HootOutAPI
{
    public class RegistrationContainer : IRegistrationContainer
    {
        public void Register(ContainerBuilder builder)
        {
            builder.RegisterType<FileClassifier>().AsSelf().SingleInstance();
            builder.RegisterType<DataInit>().As<IDefaultValues>().SingleInstance();
        }
    }
}
