using HootOut.CommonDomain.Persistence;
using Npgsql;
using System.Data.Common;

namespace HootOut.PostgreSQL.Services
{
    public class PostgreSQLProvider : IPersistenceProvider
    {
        public DbConnection GetNewConnection()
        {
            var connection = new NpgsqlConnection(Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection"));
            connection.Open();
            return connection;
        }
    }
}
