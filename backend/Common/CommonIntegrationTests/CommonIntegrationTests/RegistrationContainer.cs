using Autofac;
using HootOut.CommonIntegrationTests.PostgreSQL;
using HootOut.Infraestructure.DI;

namespace HootOut.CommonIntegrationTests
{
    internal class RegistrationContainer : IRegistrationContainer
    {
        public void Register(ContainerBuilder builder)
        {
            builder.RegisterType<ClearAllTables>().AsSelf().SingleInstance();
        }
    }
}
