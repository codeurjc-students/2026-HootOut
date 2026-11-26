using HootOut.Contracts.MediaFile.Dtos;
using HootOut.Contracts.MediaFile.Services;
using HootOut.MediaFiles.Mappers;
using HootOut.MediaFiles.Repositories;

namespace HootOut.MediaFiles.Services
{
    public class MediaFileService : IMediaFileService
    {
        private readonly IMediaFileRepository mediaFileRepository;

        public MediaFileService(IMediaFileRepository mediaFileRepository)
        {
            this.mediaFileRepository = mediaFileRepository ?? throw new ArgumentNullException(nameof(mediaFileRepository));
        }

        public async Task<MediaFileDto?> GetMediaFileByIdAsync(Guid mediaFileId, CancellationToken ct)
        {
            return (await mediaFileRepository.GetMediaFileByIdAsync(mediaFileId, ct))?.ToDto();
        }
    }
}
