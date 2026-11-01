using Dapper;
using HootOut.ChatServers.Entities;
using HootOut.CommonDomain.Persistence;
using HootOut.Contracts.Common.Saver;
using System;
using System.Collections.Generic;
using System.Text;

namespace HootOut.PostgreSQL.Savers.ChatServers
{
    public class ServerInvitationSaver: ISaver<ServerInvitation>
    {
        private readonly IPersistenceProvider provider; 

        public ServerInvitationSaver(IPersistenceProvider provider)
        {
            this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        public async Task SaveAsync(ServerInvitation item, CancellationToken ct = default) { 
            await using var conn = await provider.OpenConnectionAsync(ct);
            await using var tx = await conn.BeginTransactionAsync(ct);

            try
            {
                await conn.ExecuteAsync(
                   new CommandDefinition("""
                INSERT INTO "HootOut"."ServerInvitation" ("Id", "ChatServerId", "InvitedBy", "UserId", "Status", "CreatedAt", "ModifiedAt") 
                   VALUES (@Id, @ChatServerId, @InvitedBy, @UserId, @Status, @CreatedAt, @ModifiedAt)
                ON CONFLICT ("Id") DO UPDATE
                       SET "Status" = @Status,
                           "ModifiedAt" = @ModifiedAt
               """, item, tx, cancellationToken: ct));

                await tx.CommitAsync(ct);
            }
            catch (Exception ex)
            {
                var msg = $"Error while trying to save server invitation {item.Id}";
                throw new Exception(msg, ex);
            } 
        } 
    }
}
