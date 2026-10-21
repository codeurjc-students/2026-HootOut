using Autofac;
using Autofac.Extensions.DependencyInjection;
using HootOut.CommonDomain.Persistence;
using HootOut.CommonIntegrationTests;
using HootOut.CommonIntegrationTests.S3;
using HootOut.CommonIntegrationTests.Services;
using HootOut.FileStorage.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;

namespace HootOut.HootOutAPI.IntegrationTests.Common
{
    public class APIFixture : WebApplicationFactory<Program>, IAsyncLifetime
    {
        public ILifetimeScope AutofacRoot => Services.GetAutofacRoot();
        public TestContainerPostgreSQLProvider? PostgreSQLProvider { get; set; }
        public TestS3Container s3TestContainer;
        private const string bucketName = "test";
        protected override IHostBuilder CreateHostBuilder()
        {
            TestRegistrationManager testRegistrationManager = new TestRegistrationManager();
            testRegistrationManager.RegisterTest = (builder) =>
            {
                builder?.RegisterInstance(PostgreSQLProvider!).As<IPersistenceProvider>().SingleInstance();

                var optionsMock = new Mock<IOptions<FileStorageSettings>>();
                optionsMock.Setup(x => x.Value).Returns(new FileStorageSettings
                {
                    S3Bucket = bucketName,
                    MaxFileBytes = 2000
                });
                builder?.RegisterInstance(optionsMock.Object).As<IOptions<FileStorageSettings>>().SingleInstance();
            };

            return Host.CreateDefaultBuilder()
                .UseServiceProviderFactory(new AutofacServiceProviderFactory())
                .ConfigureServices(services =>
                {
                    services.AddControllers().AddApplicationPart(typeof(Startup).Assembly);
                })
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>((webHostBuilder) =>
                    {
                        var startup = new Startup(webHostBuilder.Configuration);
                        startup.RegistrationManager = testRegistrationManager;
                        return startup;
                    });
                });
        }

        public HttpClient CreateHttpsClient()
        {
            return CreateClient(new()
            {
                BaseAddress = new Uri("https://localhost"),
                HandleCookies = false,
            });
        }

        protected override IHost CreateHost(IHostBuilder builder)
        {
            return base.CreateHost(builder);
        }

        public async ValueTask InitializeAsync()
        {
            PostgreSQLProvider = new TestContainerPostgreSQLProvider();
            await PostgreSQLProvider.InitializeAsync();

            s3TestContainer = new TestS3Container();
            await s3TestContainer.InitializeAsync();
        }

        public override async ValueTask DisposeAsync()
        {
            await base.DisposeAsync();
            await PostgreSQLProvider!.DisposeAsync();
            await s3TestContainer.DisposeAsync();
            GC.SuppressFinalize(this);
        }
    }
}