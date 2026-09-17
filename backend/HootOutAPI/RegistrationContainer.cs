using Autofac;
using HootOut.CommonDomain.DefaultValues;
using HootOut.HootOutAPI.Temporal;
using HootOut.Infraestructure.DI; 

namespace HootOut.HootOutAPI
{
    public class RegistrationContainer : IRegistrationContainer
    {
        public void Register(ContainerBuilder builder)
        {
            builder.RegisterType<TemporalDBInitialization>().As<IDefaultValues>().SingleInstance();
        }
    }
}
