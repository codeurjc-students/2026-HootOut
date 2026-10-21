using Dapper;
using HootOut.CommonDomain.Persistence;
using HootOut.Contracts.Common.Saver;
using HootOut.MediaFiles.Entities;

namespace HootOut.PostgreSQL.Savers.MediaFiles
{
    public sealed class MediaFileSaver : ISaver<MediaFile>
    {
        private readonly IPersistenceProvider provider;

        public MediaFileSaver(IPersistenceProvider provider)
        {
            this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        private const string addMediaFile = """
            INSERT INTO "HootOut"."MediaFile" 
            ("Id", "FileName", "Checksum", "BytesSize", "OwnerId", "Url", "ContentType", "CreatedAt", "ModifiedAt")
            VALUES (@Id, @FileName, @Checksum, @BytesSize, @OwnerId, @Url, @ContentType, @CreatedAt, @ModifiedAt)
            """;

        public async Task SaveAsync(MediaFile item, CancellationToken ct = default)
        {
            await using var conn = await provider.OpenConnectionAsync(ct);
            await using var tx = await conn.BeginTransactionAsync(ct);
            try
            {
                await conn.ExecuteAsync(new CommandDefinition(addMediaFile, new
                {
                    item.Id,
                    item.FileName,
                    item.Checksum,
                    item.BytesSize,
                    item.OwnerId,
                    item.Url,
                    item.ContentType,
                    item.CreatedAt,
                    item.ModifiedAt
                }, tx, cancellationToken: ct));
                await tx.CommitAsync(ct);
            }
            catch (Exception ex)
            {
                var msg = $"Error while trying to save the chat server {item.Id}";
                throw new Exception(msg, ex);
            }
        }
    }
}
