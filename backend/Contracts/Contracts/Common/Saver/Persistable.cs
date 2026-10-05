namespace HootOut.Contracts.Common.Saver
{
    public class Persistable
    {
        public Guid Id { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime ModifiedAt { get; set; }

        public bool Deleted { get; set; }

        public static T CreateNew<T>() where T : Persistable, new()
        {
            DateTime now = DateTime.UtcNow;
            return new T()
            {
                Id = Guid.CreateVersion7(),
                CreatedAt = now,
                ModifiedAt = now,
            };
        }

    }
}
