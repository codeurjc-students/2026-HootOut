using Autofac;
using Autofac.Extensions.DependencyInjection;
using HootOut.CommonDomain.Persistence;
using HootOut.CommonIntegrationTests.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;

namespace HootOut.HootOutAPI.IntegrationTests.Common
{
    public class APIFixture : WebApplicationFactory<Program>, IAsyncLifetime
    {
        public ILifetimeScope AutofacRoot => Services.GetAutofacRoot();
        public TestContainerPostgreSQLProvider postgreSQLProvider { get; set; }
        protected override IHostBuilder CreateHostBuilder()
        {
            TestRegistrationManager testRegistration = new TestRegistrationManager();
            testRegistration.RegisterTest = (builder) =>
            {
                builder.RegisterInstance(postgreSQLProvider).As<IPersistenceProvider>().SingleInstance();
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
            return postgreSQLProvider.DisposeAsync();
        }

        public ValueTask InitializeAsync()
        {
            postgreSQLProvider = new TestContainerPostgreSQLProvider();
            return postgreSQLProvider.InitializeAsync();
        }
    }
}