using HootOut.Contracts.MediaFile.Dtos;

namespace HootOut.CommonDomain.FileStorage
{
    public interface IFileStorageService
    {
        Task<MediaFileDto> UploadFileAsync(string bucketName, string fileName, Stream fileStream, string contentType, Guid? uploader, CancellationToken ct = default);

        Task<(Stream, string, string)> GetFileAsync(Guid id, CancellationToken ct = default);
    }
}
