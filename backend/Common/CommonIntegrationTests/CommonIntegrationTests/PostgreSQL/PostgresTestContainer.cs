
using Autofac;
using HootOut.CommonDomain.Persistence;
using HootOut.CommonIntegrationTests.Services;
using HootOut.Infraestructure.DI;
using Xunit;

namespace HootOut.CommonIntegrationTests.PostgreSQL
{
    public class PostgresTestContainer : IAsyncLifetime
    {
        public TestContainerPostgreSQLProvider? postgreSQLProvider { get; set; }

        public async ValueTask InitializeAsync()
        {
            if (postgreSQLProvider == null)
            {
                postgreSQLProvider = new TestContainerPostgreSQLProvider();
                await postgreSQLProvider.InitializeAsync();
            }
        }

        public async ValueTask DisposeAsync()
        {
            await postgreSQLProvider!.DisposeAsync();
            GC.SuppressFinalize(this);
        }

        public ContainerBuilder RegisterTestPersistenceProvider()
        {
            var builder = new ContainerBuilder();
            new RegistrationManager().RegisterAllAssemblies(builder);
            builder.RegisterInstance(this.postgreSQLProvider!).As<IPersistenceProvider>().SingleInstance();
            return builder;
        }
    }
}
