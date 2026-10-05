

namespace HootOut.Contracts.Common.Saver
{
    public interface ISaver<T> where T : Persistable
    {
        void Save(T item);

        void SaveMany(IEnumerable<T> item)
        {
            // This mehtod is optional
        }
    }
}
