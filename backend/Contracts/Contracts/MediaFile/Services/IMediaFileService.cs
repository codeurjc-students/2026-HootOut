using HootOut.Contracts.MediaFile.Dtos;

namespace HootOut.Contracts.MediaFile.Services
{
    public interface IMediaFileService
    {
        Task<MediaFileDto?> GetMediaFileByIdAsync(Guid mediaFileId, CancellationToken ct);
    }
}
