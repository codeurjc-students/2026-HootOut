
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

        public ValueTask DisposeAsync()
        {
            return postgreSQLProvider?.DisposeAsync() ?? ValueTask.CompletedTask;
        }

        public ValueTask InitializeAsync()
        {
            if (postgreSQLProvider == null)
            {
            postgreSQLProvider = new TestContainerPostgreSQLProvider();
            return postgreSQLProvider.InitializeAsync(); 
            }
            return ValueTask.CompletedTask;
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
