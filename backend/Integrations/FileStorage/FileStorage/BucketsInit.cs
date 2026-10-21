using Amazon.S3;
using Amazon.S3.Model;
using HootOut.CommonDomain.DefaultValues;
using HootOut.FileStorage.Configuration;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Text;

namespace HootOut.FileStorage
{
    public sealed class BucketsInit : IDefaultValues
    {
        private readonly IS3ClientProvider s3ClientProvider;
        private readonly FileStorageSettings settings;

        public BucketsInit(IS3ClientProvider s3ClientProvider, IOptions<FileStorageSettings> settings)
        {
            this.s3ClientProvider = s3ClientProvider ?? throw new ArgumentNullException(nameof(s3ClientProvider));
            this.settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
        }

        public async Task InitAsync(CancellationToken ct = default)
        {
            var client = s3ClientProvider.GetS3Client();
            ArgumentNullException.ThrowIfNull(client);

            await client.PutBucketAsync(new PutBucketRequest
            {
                BucketName = settings.S3Bucket
            }, ct);
        }
    }
}
