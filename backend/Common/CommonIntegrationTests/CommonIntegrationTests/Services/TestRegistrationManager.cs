using Autofac;
using HootOut.Infraestructure.DI;

namespace HootOut.CommonIntegrationTests.Services
{
    public class TestRegistrationManager : RegistrationManager, IRegistrationManager
    {
        public Action<ContainerBuilder> RegisterTest { get; set; } = (x) => { };
        public override ContainerBuilder RegisterAllAssemblies(ContainerBuilder builder)
        {
            base.RegisterAllAssemblies(builder);
            RegisterTest.Invoke(builder);
            return builder;
        }
    }
}