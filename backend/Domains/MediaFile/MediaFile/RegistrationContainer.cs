using Autofac;
using HootOut.Contracts.MediaFile.Services;
using HootOut.Infraestructure.DI;
using HootOut.MediaFiles.Services;

namespace HootOut.MediaFiles
{
    public sealed class RegistrationContainer : IRegistrationContainer
    {
        public void Register(ContainerBuilder builder)
        {
            builder.RegisterType<MediaFileService>().As<IMediaFileService>().SingleInstance();
        }
    }
}
