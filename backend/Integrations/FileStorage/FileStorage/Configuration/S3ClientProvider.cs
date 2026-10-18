using Amazon.Runtime;
using Amazon.S3;

namespace HootOut.FileStorage.Configuration
{
    public sealed class S3ClientProvider : IS3ClientProvider
    {
        private IAmazonS3? s3Client;

        public IAmazonS3 GetS3Client()
        {
            if (s3Client == null)
            {
                var config = new AmazonS3Config
                {
                    ServiceURL = Environment.GetEnvironmentVariable("S3_URL") ?? throw new InvalidOperationException("Environment variable S3_URL is null"),
                    ForcePathStyle = true,
                    RequestChecksumCalculation = Amazon.Runtime.RequestChecksumCalculation.WHEN_REQUIRED,
                };

                s3Client = new AmazonS3Client(
                    new BasicAWSCredentials(
                        Environment.GetEnvironmentVariable("S3_ACCESS_KEY") ?? throw new InvalidOperationException("Environment variable S3_ACCESS_KEY is null"),
                        Environment.GetEnvironmentVariable("S3_SECRET_KEY") ?? throw new InvalidOperationException("Environment variable S3_SECRET_KEY is null")
                    )
                , config);
            }

            return s3Client;
        }
    }
}
