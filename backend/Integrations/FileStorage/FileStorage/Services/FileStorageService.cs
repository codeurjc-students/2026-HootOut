using Amazon.S3.Model;
using FluentValidation;
using HootOut.CommonDomain.FileStorage;
using HootOut.Contracts.Common.Saver;
using HootOut.Contracts.MediaFile.Responses;
using HootOut.FileStorage.Configuration;
using HootOut.MediaFiles.Entities;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;

namespace HootOut.FileStorage.Services
{
    public sealed class FileStorageService : IFileStorageService
    {
        private readonly IS3ClientProvider s3ClientProvider;
        private readonly ISaver<MediaFile> saver;
        private readonly IMediaUrlService mediaUrlService;
        private readonly FileStorageSettings fileStorageSettings;

        public FileStorageService(IS3ClientProvider s3ClientProvider, ISaver<MediaFile> saver, IOptions<FileStorageSettings> options, IMediaUrlService mediaUrlService)
        {
            this.s3ClientProvider = s3ClientProvider ?? throw new ArgumentNullException(nameof(s3ClientProvider));
            this.saver = saver ?? throw new ArgumentNullException(nameof(saver));
            this.fileStorageSettings = options?.Value ?? throw new ArgumentNullException(nameof(options));
            this.mediaUrlService = mediaUrlService ?? throw new ArgumentNullException(nameof(mediaUrlService));
        }

        public async Task<UploadFileResponse> UploadFileAsync(string fileName, Stream fileStream, string contentType, Guid? uploader, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(fileStream);

            var bytesSize = fileStream.Length;

            if (bytesSize > fileStorageSettings.MaxFileBytes)
            {
                throw new ValidationException($"File size exceeds {fileStorageSettings.MaxFileBytes}");
            }

            MediaFile newFile = Persistable.CreateNew<MediaFile>();

            var sha = await SHA256.HashDataAsync(fileStream, ct);
            fileStream.Position = 0;
            var hex = Convert.ToHexString(sha).ToLowerInvariant();

            PutObjectRequest request = new PutObjectRequest
            {
                BucketName = fileStorageSettings.S3Bucket,
                Key = newFile.Id.ToString(),
                InputStream = fileStream,
                ContentType = contentType,
                ChecksumSHA256 = Convert.ToBase64String(sha),
            };

            var response = await s3ClientProvider.GetS3Client().PutObjectAsync(request, ct);

            newFile.FileName = fileName;
            newFile.Checksum = hex;
            newFile.BytesSize = bytesSize;
            newFile.OwnerId = uploader;
            newFile.ContentType = contentType;

            await saver.SaveAsync(newFile, ct);

            string signedUrl = await mediaUrlService.GetViewUrlAsync(newFile.Id, newFile.ContentType, newFile.FileName) ?? throw new Exception($"Could not get view signed url for file {newFile.Id}");
            string downloadUrl = await mediaUrlService.GetDownloadUrlAsync(newFile.Id, newFile.ContentType, newFile.FileName) ?? throw new Exception($"Could not download get signed url for file {newFile.Id}");

            return new UploadFileResponse
            {
                Id = newFile.Id,
                ContentType = newFile.ContentType,
                BytesSize = bytesSize,
                FileName = newFile.FileName,
                Url = signedUrl,
                DownloadUrl = downloadUrl
            };
        }
    }
}
