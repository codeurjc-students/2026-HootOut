using Autofac;
using HootOut.CommonDomain.FileStorage;
using HootOut.FileStorage.Configuration;
using HootOut.FileStorage.Services;
using HootOut.Infraestructure.DI;

namespace HootOut.FileStorage
{
    public sealed class RegistrationContainer : IRegistrationContainer
    {
        public void Register(ContainerBuilder builder)
        {
            builder.RegisterType<S3ClientProvider>().As<IS3ClientProvider>().SingleInstance();
            builder.RegisterType<FileStorageService>().As<IFileStorageService>().SingleInstance();
        }
    }
}