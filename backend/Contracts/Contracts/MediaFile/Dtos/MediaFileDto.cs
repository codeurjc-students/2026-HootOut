namespace HootOut.Contracts.MediaFile.Dtos
{
    public sealed record MediaFileDto
    {
        public Guid Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string Md5 { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public Guid? OwnerId { get; set; }
    }
}
