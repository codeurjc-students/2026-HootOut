namespace HootOut.Contracts.Common.Saver
{
    public abstract class Persistable
    {
        public Guid Uid { get; set; }

        public DateTime CreatedDate { get; set; }

        public DateTime ModifiedDate { get; set; }
    }
}
