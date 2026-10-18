using HootOut.MediaFiles.Entities;

namespace HootOut.MediaFiles.Search
{
    public interface IMediaFileSearch
    {
        Task<MediaFile?> GetMediaFileByIdAsync(Guid id, CancellationToken ct = default);
    }
}
