using Autofac;

namespace HootOut.Infraestructure.DI
{
    public interface IRegistrationManager
    {
        ContainerBuilder RegisterAllAssemblies(ContainerBuilder builder);
    }
}
