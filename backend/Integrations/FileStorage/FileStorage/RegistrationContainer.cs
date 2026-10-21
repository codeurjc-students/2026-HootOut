using Autofac;
using HootOut.CommonDomain.DefaultValues;
using HootOut.CommonDomain.FileStorage;
using HootOut.FileStorage.Configuration;
using HootOut.FileStorage.Services;
using HootOut.Infraestructure.DI;
using Microsoft.Extensions.Caching.Memory;

namespace HootOut.FileStorage
{
    public sealed class RegistrationContainer : IRegistrationContainer
    {
        public void Register(ContainerBuilder builder)
        {
            builder.RegisterType<BucketsInit>().As<IDefaultValues>().SingleInstance();
            builder.RegisterType<S3ClientProvider>().As<IS3ClientProvider>().SingleInstance();
            builder.RegisterType<FileStorageService>().As<IFileStorageService>().SingleInstance();
            builder.RegisterType<MediaUrlService>().As<IMediaUrlService>().SingleInstance();

            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            builder.RegisterInstance(memoryCache).As<IMemoryCache>().SingleInstance();
        }
    }
}