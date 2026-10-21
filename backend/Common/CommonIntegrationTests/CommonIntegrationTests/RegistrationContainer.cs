using Autofac;
using HootOut.CommonIntegrationTests.PostgreSQL;
using HootOut.FileStorage.Configuration;
using HootOut.Infraestructure.DI;
using Microsoft.Extensions.Options;
using Moq;

namespace HootOut.CommonIntegrationTests
{
    internal class RegistrationContainer : IRegistrationContainer
    {
        public void Register(ContainerBuilder builder)
        {
            builder.RegisterType<ClearAllTables>().AsSelf().SingleInstance();

            var optionsMock = new Mock<IOptions<FileStorageSettings>>();
            optionsMock.Setup(x => x.Value).Returns(new FileStorageSettings
            {
                S3Bucket = "test",
                MaxFileBytes = 2000
            });

            builder.RegisterInstance(optionsMock.Object).As<IOptions<FileStorageSettings>>().SingleInstance();
        }
    }
}
