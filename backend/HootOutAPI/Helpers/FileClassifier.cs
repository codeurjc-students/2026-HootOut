using MimeDetective;

namespace HootOut.HootOutAPI.Helpers
{
    public sealed class FileClassifier
    {
        private readonly IContentInspector inspector;
        public FileClassifier(IContentInspector inspector)
        {
            this.inspector = inspector ?? throw new ArgumentNullException(nameof(inspector));
        }

        //private readonly

        public async Task<string?> GetMimeTypeAsync(IFormFile file, CancellationToken ct = default)
        {
            var buffer = new byte[8192];

            await using var stream = file.OpenReadStream();
            var read = await stream.ReadAtLeastAsync(buffer, buffer.Length, throwOnEndOfStream: false, cancellationToken: ct);

            var results = inspector.Inspect(buffer.AsSpan(0, read).ToArray());
            var mime = results.ByMimeType().FirstOrDefault()?.MimeType;

            mime ??= Path.GetExtension(file.FileName).ToLowerInvariant() switch
            {
                ".txt" => "text/plain",
                ".csv" => "text/csv",
                _ => null
            };

            return mime;
        }
    }
}
