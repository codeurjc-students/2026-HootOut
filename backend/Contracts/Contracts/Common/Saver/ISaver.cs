

namespace HootOut.Contracts.Common.Saver
{
    public interface ISaver<T> where T : Persistable
    {
        async Task SaveAsync(T item, CancellationToken ct = default) { }

        async Task SaveManyAsync(IEnumerable<T> items, CancellationToken ct = default) { }

        async Task DeleteAsync(T item, CancellationToken ct = default) { }
    }
}
