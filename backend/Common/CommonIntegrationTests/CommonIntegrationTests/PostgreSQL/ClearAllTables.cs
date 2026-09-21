using Dapper;
using HootOut.CommonDomain.Persistence;
using HootOut.CommonIntegrationTests.Services;

namespace HootOut.CommonIntegrationTests.PostgreSQL
{
    public class ClearAllTables
    {
        private IPersistenceProvider persistenceProvider;

        public ClearAllTables(IPersistenceProvider persistenceProvicer)
        {
            this.persistenceProvider = persistenceProvicer ?? throw new ArgumentNullException(nameof(persistenceProvicer));
        }

        public void ClearTables()
        {
            if (persistenceProvider is not TestContainerPostgreSQLProvider)
            {
                throw new InvalidOperationException("The persistence provider is not the Testing persistence provider.");
            }

            using (var conn = persistenceProvider.GetNewConnection())
            {
                conn.Execute("""TRUNCATE TABLE "HootOut"."UserInfo";""");
            }
        }
    }
}
