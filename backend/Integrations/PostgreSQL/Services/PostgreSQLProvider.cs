using HootOut.CommonDomain.Persistence;
using Npgsql;
using System.Data.Common;

namespace HootOut.PostgreSQL.Services
{
    public class PostgreSQLProvider : IPersistenceProvider
    {
        public DbConnection OpenConnection()
        {
            var connection = new NpgsqlConnection(Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection"));
            connection.Open();
            return connection;
        }

        public async Task<DbConnection> OpenConnectionAsync(CancellationToken ct)
        {
            var connection = new NpgsqlConnection(Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection"));
            await connection.OpenAsync(ct);
            return connection;
        }
    }
}
