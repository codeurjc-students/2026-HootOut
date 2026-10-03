namespace HootOut.Contracts.Common.Saver
{
    public abstract class Persistable
    {
        public Guid Id { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime ModifiedAt { get; set; }

        public bool Deleted { get; set; }
    }
}
