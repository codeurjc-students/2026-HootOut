namespace HootOut.Contracts.ChatMessage.Dtos
{
    public sealed record ChatMessageAttachmentDto
    {
        public Guid Id { get; set; }
        public Guid MessageId { get; set; }
        public Guid MediaFileId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string DownloadUrl { get; set; } = string.Empty;
    }
}
