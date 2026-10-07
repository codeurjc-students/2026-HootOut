using System.Data.Common;

namespace HootOut.CommonDomain.Persistence
{
    public interface IPersistenceProvider
    {
        DbConnection GetNewConnection();

        Task<DbConnection> GetConnectionAsync(CancellationToken ct);
    }
}
