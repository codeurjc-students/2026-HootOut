using System.Data.Common;

namespace HootOut.CommonDomain.Persistence
{
    public interface IPersistenceProvider
    {
        DbConnection OpenConnection();

        Task<DbConnection> OpenConnectionAsync(CancellationToken ct);
    }
}
