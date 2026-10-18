using Dapper;
using HootOut.ChatChannels.ChatChannels;
using HootOut.CommonDomain.Persistence;
using HootOut.MediaFiles.Entities;
using HootOut.MediaFiles.Search;
using System.Threading.Channels;

namespace HootOut.PostgreSQL.Searchs.MediaFiles
{
    public sealed class MediaFileSearch : IMediaFileSearch
    {
        private readonly IPersistenceProvider provider;

        public MediaFileSearch(IPersistenceProvider provider)
        {
            this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        private const string mediaFileColumns =
            """ "Id", "FileName", "Checksum", "ContentType", "BytesSize", "Url", "OwnerId", "CreatedAt", "ModifiedAt" """;

        public async Task<MediaFile?> GetMediaFileByIdAsync(Guid id, CancellationToken ct = default)
        {
            await using var conn = await provider.OpenConnectionAsync(ct);

            return (await conn.QueryAsync<MediaFile>(new CommandDefinition(
               $"""
                    SELECT
                        {mediaFileColumns}
                    FROM "HootOut"."MediaFile"
                    WHERE "Id" = @Id AND "Deleted" = False
                """,
                new { Id = id },
                cancellationToken: ct))).FirstOrDefault();
        }
    }
}
