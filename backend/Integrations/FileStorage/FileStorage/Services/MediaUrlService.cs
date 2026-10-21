using Amazon.S3;
using Amazon.S3.Model;
using HootOut.CommonDomain.FileStorage;
using HootOut.FileStorage.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;

namespace HootOut.FileStorage.Services
{
    public sealed class MediaUrlService : IMediaUrlService
    {
        private static readonly TimeSpan UrlLifetime = TimeSpan.FromHours(1);
        private readonly IMemoryCache cache;
        private readonly IS3ClientProvider s3ClientProvider;
        private readonly FileStorageSettings fileStorageSettings;

        public MediaUrlService(IMemoryCache cache, IS3ClientProvider s3ClientProvider, IOptions<FileStorageSettings> options)
        {
            this.cache = cache ?? throw new ArgumentNullException(nameof(cache));
            this.s3ClientProvider = s3ClientProvider ?? throw new ArgumentNullException(nameof(s3ClientProvider));
            this.fileStorageSettings = options?.Value ?? throw new ArgumentNullException(nameof(options));
        }

        public async Task<string?> GetViewUrlAsync(Guid fileId, string contentType, string fileName) => await GetUrlAsync(fileId, contentType, fileName, "view", "inline");

        public async Task<string?> GetDownloadUrlAsync(Guid fileId, string contentType, string fileName) => await GetUrlAsync(fileId, contentType, fileName, "download", AttachmentDisposition(fileName));

        private async Task<string?> GetUrlAsync(Guid fileId, string contentType, string fileName, string kind, string disposition)
        {
            return await cache.GetOrCreateAsync($"media-url:{fileId}:{kind}", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = UrlLifetime / 2;
                return await SignAsync(fileId, contentType, fileName, disposition);
            });
        }

        private async Task<string?> SignAsync(Guid fileId, string contentType, string fileName, string disposition)
        {
            var request = new GetPreSignedUrlRequest
            {
                BucketName = fileStorageSettings.S3Bucket,
                Key = fileId.ToString(),
                Verb = HttpVerb.GET,
                Expires = DateTime.UtcNow.Add(UrlLifetime),
                Protocol = Protocol.HTTPS
            };

            request.ResponseHeaderOverrides.ContentType = contentType;
            request.ResponseHeaderOverrides.ContentDisposition = disposition;
            request.ResponseHeaderOverrides.CacheControl = "private, max-age=1800";

            return await s3ClientProvider.GetPresignS3Client().GetPreSignedURLAsync(request);
        }
        private static string AttachmentDisposition(string fileName) =>
            new ContentDispositionHeaderValue("attachment")
            {
                FileName = $"\"{AsciiFallback(fileName)}\"",
                FileNameStar = fileName,
            }.ToString();

        private static string AsciiFallback(string name) =>
            new string(name.Select(c => c < 128 && c != '"' && c != '\\' ? c : '_').ToArray());
    }


}
