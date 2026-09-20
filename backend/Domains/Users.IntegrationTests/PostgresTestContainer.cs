using HootOut.CommonIntegrationTests.Services;
using System;
using System.Collections.Generic;
using System.Text;

namespace HootOut.Users.IntegrationTests
{
    public class PostgresTestContainer : IAsyncLifetime
    {
        public TestContainerPostgreSQLProvider postgreSQLProvider { get; set;  }

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
