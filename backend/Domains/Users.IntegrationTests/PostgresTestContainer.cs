
using Autofac;
using HootOut.CommonDomain.Persistence;
using HootOut.CommonIntegrationTests.Services;
using HootOut.Infraestructure.DI;

namespace HootOut.Users.IntegrationTests
{
    public class PostgresTestContainer : IAsyncLifetime
    {
        public TestContainerPostgreSQLProvider postgreSQLProvider { get; set; }

        public ValueTask DisposeAsync()
        {
            return postgreSQLProvider.DisposeAsync();
        }

        public ValueTask InitializeAsync()
        {
            postgreSQLProvider = new TestContainerPostgreSQLProvider();
            return postgreSQLProvider.InitializeAsync();
        }

        public ContainerBuilder RegisterTestPersistenceProvider()
        {
            var builder = new ContainerBuilder();
            new RegistrationManager().RegisterAllAssemblies(builder);
            builder.RegisterInstance(this.postgreSQLProvider).As<IPersistenceProvider>().SingleInstance();
            return builder;
        }
    }
}
