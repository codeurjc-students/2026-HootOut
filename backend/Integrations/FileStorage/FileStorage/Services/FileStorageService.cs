using Amazon.S3;
using Amazon.S3.Model;
using FluentValidation;
using HootOut.CommonDomain.FileStorage;
using HootOut.Contracts.Common.Saver;
using HootOut.Contracts.MediaFile.Dtos;
using HootOut.FileStorage.Configuration;
using HootOut.MediaFiles.Entities;
using HootOut.MediaFiles.Mappers;
using HootOut.MediaFiles.Search;
using System.Security.Cryptography;

namespace HootOut.FileStorage.Services
{
    public sealed class FileStorageService : IFileStorageService
    {
        private readonly IS3ClientProvider s3ClientProvider;
        private readonly ISaver<MediaFile> saver;
        private readonly IMediaFileSearch search;

        private const long maxFileSizeBytes = 30 * 1024 * 1024;

        public FileStorageService(IS3ClientProvider s3ClientProvider, ISaver<MediaFile> saver, IMediaFileSearch search)
        {
            this.s3ClientProvider = s3ClientProvider ?? throw new ArgumentNullException(nameof(s3ClientProvider));
            this.saver = saver ?? throw new ArgumentNullException(nameof(saver));
            this.search = search ?? throw new ArgumentNullException(nameof(search));
        }

        public async Task<MediaFileDto> UploadFileAsync(string bucketName, string fileName, Stream fileStream, string contentType, Guid? uploader, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(fileStream);

            var bytesSize = fileStream.Length;

            if (bytesSize > maxFileSizeBytes)
            {
                throw new ValidationException($"File size exceeds {maxFileSizeBytes}");
            }

            MediaFile newFile = Persistable.CreateNew<MediaFile>();

            var sha = await SHA256.HashDataAsync(fileStream, ct);
            fileStream.Position = 0;
            var hex = Convert.ToHexString(sha).ToLowerInvariant();   

            PutObjectRequest request = new PutObjectRequest
            {
                BucketName = bucketName,
                Key = newFile.Id.ToString(),
                InputStream = fileStream,
                ContentType = contentType,
                ChecksumSHA256 = Convert.ToBase64String(sha),
            };

            var response = await s3ClientProvider.GetS3Client().PutObjectAsync(request, ct);

            newFile.FileName = fileName;
            newFile.Url = $"{bucketName}/{newFile.Id}";
            newFile.Checksum = hex;
            newFile.BytesSize = bytesSize;
            newFile.OwnerId = uploader;
            newFile.ContentType = contentType;

            await saver.SaveAsync(newFile, ct);

            return newFile.ToDto();
        }

        public async Task<(Stream, string, string)> GetFileAsync(Guid id, CancellationToken ct = default)
        {
            var mediaFile = await search.GetMediaFileByIdAsync(id, ct);

            if (mediaFile == null)
            {
                return (null, null, null);
            }

            GetObjectRequest request = new GetObjectRequest()
            {
                Key = mediaFile.Id.ToString(),
                BucketName = mediaFile.Url.Split('/').FirstOrDefault()
            };

            var response = await s3ClientProvider.GetS3Client().GetObjectAsync(request, ct);

            return (response.ResponseStream, mediaFile.ContentType, mediaFile.FileName);
        }
    }
}
