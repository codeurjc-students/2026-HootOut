using Autofac;
using Autofac.Extensions.DependencyInjection;
using HootOut.CommonDomain.Persistence;
using HootOut.CommonIntegrationTests.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting; 

namespace HootOut.HootOutWebsockets.IntegrationTests.Common
{
    public class WebSocketServerFixture : WebApplicationFactory<Program>, IAsyncLifetime
    {
        public ILifetimeScope AutofacRoot => Services.GetAutofacRoot();
        public TestContainerPostgreSQLProvider? PostgreSQLProvider { get; set; }
        protected override IHostBuilder CreateHostBuilder()
        {
            TestRegistrationManager testRegistration = new TestRegistrationManager();
            testRegistration.RegisterTest = (builder) =>
            {
                builder?.RegisterInstance(PostgreSQLProvider!).As<IPersistenceProvider>().SingleInstance();
            };

            Startup.RegistrationManager = testRegistration;

            return Host.CreateDefaultBuilder()
                .UseServiceProviderFactory(new AutofacServiceProviderFactory())
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
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
