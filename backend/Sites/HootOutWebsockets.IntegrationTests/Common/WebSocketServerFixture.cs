using Autofac;
using Autofac.Extensions.DependencyInjection;
using HootOut.CommonDomain.Persistence;
using HootOut.CommonIntegrationTests;
using HootOut.CommonIntegrationTests.S3;
using HootOut.CommonIntegrationTests.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace HootOut.HootOutWebsockets.IntegrationTests.Common
{
    public class WebSocketServerFixture : WebApplicationFactory<Program>, IAsyncLifetime
    {
        public ILifetimeScope AutofacRoot => Services.GetAutofacRoot();
        public TestContainerPostgreSQLProvider? PostgreSQLProvider { get; set; }
        public TestS3Container? TestS3Container { get; set;  }

        public Action<ContainerBuilder?>? RegisterDependencies;
        protected override IHostBuilder CreateHostBuilder()
        {
            TestRegistrationManager testRegistrationManager = new TestRegistrationManager();
            testRegistrationManager.RegisterTest = (builder) =>
            {
                builder?.RegisterInstance(PostgreSQLProvider!).As<IPersistenceProvider>().SingleInstance();

                if (RegisterDependencies != null)
                {
                    RegisterDependencies(builder);
                }
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

        protected override IHost CreateHost(IHostBuilder builder)
        {
            return base.CreateHost(builder);
        }

        public async ValueTask InitializeAsync()
        {
            PostgreSQLProvider = new TestContainerPostgreSQLProvider();
            await PostgreSQLProvider.InitializeAsync();

            TestS3Container = new TestS3Container();
            await TestS3Container.InitializeAsync();
        }

        public new async ValueTask DisposeAsync()
        {
            await PostgreSQLProvider!.DisposeAsync();
            await TestS3Container!.DisposeAsync();
            GC.SuppressFinalize(this);
        } 
    }
}
