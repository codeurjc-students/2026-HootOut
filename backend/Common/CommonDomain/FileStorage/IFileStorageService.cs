using HootOut.Contracts.MediaFile.Responses;

namespace HootOut.CommonDomain.FileStorage
{
    public interface IFileStorageService
    {
        Task<UploadFileResponse> UploadFileAsync(string fileName, Stream fileStream, string contentType, Guid? uploader, CancellationToken ct = default);
    }
}
