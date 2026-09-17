using Autofac;

namespace HootOut.Infraestructure.DI
{
    public interface IRegistrationContainer
    {
        void Register(ContainerBuilder builder);
    }
}
