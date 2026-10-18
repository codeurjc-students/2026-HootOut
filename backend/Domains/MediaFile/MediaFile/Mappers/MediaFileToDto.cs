using HootOut.Contracts.MediaFile.Dtos;
using HootOut.MediaFiles.Entities;

namespace HootOut.MediaFiles.Mappers
{
    public static class MediaFileToDto
    {
        public static MediaFileDto ToDto(this MediaFile mediaFile)
        {
            return new MediaFileDto
            {
                Id = mediaFile.Id,
                FileName = mediaFile.FileName,
                OwnerId = mediaFile.OwnerId,
                Md5 = mediaFile.Checksum,
                Url = mediaFile.Url,
            };
        }
    }
}
