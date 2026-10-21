namespace HootOut.FileStorage.Configuration
{
    public sealed class FileStorageSettings
    {
        public string S3Bucket { get; init; } = "dev";
        public long MaxFileBytes { get; init; } = 20 * 1024 * 1024;
    }
}
