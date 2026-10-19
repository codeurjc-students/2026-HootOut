using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using HootOut.CommonDomain.FileStorage;
using HootOut.FileStorage.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Text;

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

        public async Task<string?> GetDownloadUrlAsync(Guid fileId, string contentType)
        {
            return await cache.GetOrCreateAsync($"media-url:{fileId}", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = UrlLifetime / 2;
                return await SignAsync(fileId, contentType);
            });
        }

        public async Task<string?> SignAsync(Guid fileId, string contentType)
        {
            var request = new GetPreSignedUrlRequest
            {
                BucketName = fileStorageSettings.S3Bucket,
                Key = fileId.ToString(),
                Verb = HttpVerb.GET,
                Expires = DateTime.UtcNow.Add(UrlLifetime),
                Protocol = Protocol.HTTPS
            };

            request.ResponseHeaderOverrides.ContentType = contentType; // the type you validated, not the client's
            request.ResponseHeaderOverrides.ContentDisposition = "inline";
            request.ResponseHeaderOverrides.CacheControl = "private, max-age=1800";

            return await s3ClientProvider.GetPresignS3Client().GetPreSignedURLAsync(request);
        }
    }
}
