using Autofac;
using Autofac.Extensions.DependencyInjection;
using HootOut.CommonDomain.Persistence;
using HootOut.CommonIntegrationTests;
using HootOut.CommonIntegrationTests.S3;
using HootOut.CommonIntegrationTests.Services;
using HootOut.Contracts.Common.Presence;
using HootOut.Redis.Configuration;
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
        public TestS3Container? TestS3Container { get; set; }

        /// <summary>
        /// Valkey for the presence. Here and not as a class fixture like RabbitMQ: the controller resolves
        /// IPresenceTracker on every connection, so every WebSocket test class needs it.
        /// </summary>
        public TestContainerRedisConnectionFactory? RedisConnectionFactory { get; set; }

        /// <summary>Short grace period so the offline tests do not wait 5 s.</summary>
        public PresenceOptions PresenceOptions { get; } = new() { OfflineGracePeriod = TimeSpan.FromSeconds(1) };

        public Action<ContainerBuilder?>? RegisterDependencies;
        protected override IHostBuilder CreateHostBuilder()
        {
            TestRegistrationManager testRegistrationManager = new TestRegistrationManager();
            testRegistrationManager.RegisterTest = (builder) =>
            {
                builder?.RegisterInstance(PostgreSQLProvider!).As<IPersistenceProvider>().SingleInstance();
                builder?.RegisterInstance(RedisConnectionFactory!).As<IRedisConnectionFactory>().SingleInstance();
                builder?.RegisterInstance(PresenceOptions).AsSelf().SingleInstance();

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

            RedisConnectionFactory = new TestContainerRedisConnectionFactory();
            await RedisConnectionFactory.InitializeAsync();
        }

        public new async ValueTask DisposeAsync()
        {
            await PostgreSQLProvider!.DisposeAsync();
            await TestS3Container!.DisposeAsync();
            await RedisConnectionFactory!.DisposeAsync();
            GC.SuppressFinalize(this);
        }
    }
}
