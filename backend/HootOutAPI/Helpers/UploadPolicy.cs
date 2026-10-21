namespace HootOut.HootOutAPI.Helpers
{
    public static class UploadPolicy
    {
        public static readonly HashSet<string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            // Images (no SVG: it can carry scripts)
            "image/jpeg",
            "image/png",
            "image/gif",
            "image/webp",
            "image/avif",
            "image/heic",
            "image/heif",
            "image/bmp",
            "image/tiff",

            // Video
            "video/mp4",
            "video/quicktime",
            "video/webm",
            "video/mpeg",
            "video/ogg",

            // Audio
            "audio/mpeg",
            "audio/mp4",
            "audio/mp3",
            "audio/ogg",
            "audio/wav",

            // Documents
            "application/pdf",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            "application/vnd.oasis.opendocument.text",
            "application/vnd.oasis.opendocument.spreadsheet",
            "application/vnd.oasis.opendocument.presentation",
            "text/plain",
            "text/csv",

            // Compressed
            "application/zip", 
            "application/gzip",
            "application/x-7z-compressed",
            "application/vnd.rar"
        };
    }
}
