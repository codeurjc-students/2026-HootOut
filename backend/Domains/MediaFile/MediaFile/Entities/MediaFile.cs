using HootOut.Contracts.Common.Saver;

namespace HootOut.MediaFiles.Entities
{
    public sealed class MediaFile : Persistable
    {
        public string FileName { get; set; } = string.Empty;

        public string Checksum { get; set; } = string.Empty;

        public string ContentType { get; set; } = string.Empty;

        public long BytesSize { get; set; }

        public string Url { get; set; } = string.Empty;

        public Guid? OwnerId { get; set; }
    }
}
