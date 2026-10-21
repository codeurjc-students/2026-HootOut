using Amazon.S3;

namespace HootOut.FileStorage.Configuration
{
    public interface IS3ClientProvider
    {
        IAmazonS3 GetS3Client();

        IAmazonS3 GetPresignS3Client();
    }
}
