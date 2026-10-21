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

        public async Task ClearTablesAsync(CancellationToken ct = default)
        {
            if (persistenceProvider is not TestContainerPostgreSQLProvider)
            {
                throw new InvalidOperationException("The persistence provider is not the Testing persistence provider.");
            }

            await using var conn = await persistenceProvider.OpenConnectionAsync(ct);

            await conn.ExecuteAsync("""TRUNCATE TABLE "HootOut"."UserInfo" CASCADE;""");
            await conn.ExecuteAsync("""TRUNCATE TABLE "HootOut"."ChatServer" CASCADE;""");
            await conn.ExecuteAsync("""TRUNCATE TABLE "HootOut"."ChatChannel" CASCADE;""");
            await conn.ExecuteAsync("""TRUNCATE TABLE "HootOut"."ChatMessage" CASCADE;""");
            await conn.ExecuteAsync("""TRUNCATE TABLE "HootOut"."MediaFile" CASCADE;""");
            await conn.ExecuteAsync("""TRUNCATE TABLE "HootOut"."ChatMessageAttachment" CASCADE;""");
        }
    }
}
