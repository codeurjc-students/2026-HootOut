namespace HootOut.Contracts.MediaFile.Responses
{
    public sealed record UploadFileResponse
    {
        public Guid Id { get; init; }
        public string? Url { get; set; }
        public string ContentType { get; init; } = string.Empty;
        public string FileName { get; init; } = string.Empty;
        public long BytesSize { get; init; }
    }
}
