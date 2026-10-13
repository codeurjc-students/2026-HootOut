using System.Data.Common;

namespace HootOut.CommonDomain.Persistence
{
    public interface IPersistenceProvider
    {
        Task<DbConnection> OpenConnectionAsync(CancellationToken ct);
    }
}
