namespace HootOut.CommonDomain.FileStorage
{
    public interface IMediaUrlService
    {
        Task<string?> GetDownloadUrlAsync(Guid fileId, string contentType, string fileName);
        Task<string?> GetViewUrlAsync(Guid fileId, string contentType, string fileName);
    }
}
