using HootOut.CommonIntegrationTests.Services;

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
    }
}
