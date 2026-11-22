using Dapper;
using HootOut.CommonDomain.Persistence;
using HootOut.Contracts.Common.Saver;
using HootOut.Friends.Entities;

namespace HootOut.PostgreSQL.Savers.Friends
{
    public sealed class FriendRequestSaver : ISaver<FriendRequest>
    {
        private readonly IPersistenceProvider provider;

        public FriendRequestSaver(IPersistenceProvider provider)
        {
            this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        public async Task SaveAsync(FriendRequest item, CancellationToken ct = default)
        {
            await using var conn = await provider.OpenConnectionAsync(ct);
            await using var tx = await conn.BeginTransactionAsync(ct);

            try
            {
                await conn.ExecuteAsync(
                   new CommandDefinition("""
                INSERT INTO "HootOut"."FriendRequest" ("Id", "InvitedBy", "UserId", "Status", "CreatedAt", "ModifiedAt") 
                   VALUES (@Id, @InvitedBy, @UserId, @Status, @CreatedAt, @ModifiedAt)
                ON CONFLICT ("Id") DO UPDATE
                       SET "Status" = @Status,
                           "ModifiedAt" = @ModifiedAt
               """, item, tx, cancellationToken: ct));

                await tx.CommitAsync(ct);
            }
            catch (Exception ex)
            {
                var msg = $"Error while trying to save friend request {item.Id}";
                throw new Exception(msg, ex);
            }
        }
    }
}
