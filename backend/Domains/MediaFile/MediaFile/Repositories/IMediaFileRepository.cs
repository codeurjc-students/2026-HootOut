using HootOut.MediaFiles.Entities;

namespace HootOut.MediaFiles.Repositories
{
    public interface IMediaFileRepository
    {
        Task<MediaFile?> GetMediaFileByIdAsync(Guid id, CancellationToken ct = default);
    }
}
