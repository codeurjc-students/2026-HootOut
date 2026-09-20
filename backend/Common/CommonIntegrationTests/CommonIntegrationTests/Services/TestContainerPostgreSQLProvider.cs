using HootOut.CommonDomain.Persistence;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Text;
using Testcontainers.PostgreSql;

namespace HootOut.CommonIntegrationTests.Services
{
    public class TestContainerPostgreSQLProvider : IPersistenceProvider, IAsyncDisposable
    {
        public PostgreSqlContainer Container { get; } = new PostgreSqlBuilder("postgres:18.6-trixie")
        .WithDatabase("hootout_test")
        .WithUsername("test_user")
        .WithPassword("test_password")
        .Build();

        public async ValueTask InitializeAsync() => await Container.StartAsync();

        public string ConnectionString => Container.GetConnectionString();

        public DbConnection GetNewConnection()
        {
            var connection = new NpgsqlConnection(Container.GetConnectionString());
            connection.Open();
            return connection;
        }

        public ValueTask DisposeAsync()
        {
            return Container.DisposeAsync(); 
        }
    }
}
