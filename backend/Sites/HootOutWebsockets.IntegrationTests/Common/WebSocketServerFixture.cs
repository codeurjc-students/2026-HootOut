using Autofac;
using Autofac.Extensions.DependencyInjection;
using HootOut.CommonDomain.Persistence;
using HootOut.CommonIntegrationTests;
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
        protected override IHostBuilder CreateHostBuilder()
        {
            TestRegistrationManager testRegistrationManager = new TestRegistrationManager();
            testRegistrationManager.RegisterTest = (builder) =>
            {
                builder?.RegisterInstance(PostgreSQLProvider!).As<IPersistenceProvider>().SingleInstance();
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

        public new ValueTask DisposeAsync()
        {
            return PostgreSQLProvider!.DisposeAsync();
        }

        public ValueTask InitializeAsync()
        {
            PostgreSQLProvider = new TestContainerPostgreSQLProvider();
            return PostgreSQLProvider.InitializeAsync();
        }
    }
}
