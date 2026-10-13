

namespace HootOut.Contracts.Common.Saver
{
    public interface ISaver<T> where T : Persistable
    {
        Task SaveAsync(T item, CancellationToken ct = default);
    }
}
