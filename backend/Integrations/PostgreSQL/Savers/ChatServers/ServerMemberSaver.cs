using Dapper;
using HootOut.ChatServers.Entities;
using HootOut.CommonDomain.Persistence;
using HootOut.Contracts.Common.Saver;
using System;
using System.Collections.Generic;
using System.Text;

namespace HootOut.PostgreSQL.Savers.ChatServers
{
    public class ServerMemberSaver : ISaver<ServerMember>
    {
        private readonly IPersistenceProvider provider;

        public ServerMemberSaver(IPersistenceProvider provider)
        {
            this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        public async Task SaveAsync(ServerMember item, CancellationToken ct = default)
        {
            await using var conn = await provider.OpenConnectionAsync(ct);
            await using var tx = await conn.BeginTransactionAsync(ct);

            try
            {
                await conn.ExecuteAsync(
                   new CommandDefinition("""
                INSERT INTO "HootOut"."ServerMember" ("Id", "ChatServerId", "UserId", "InvitationId", "CreatedAt", "ModifiedAt") 
                   VALUES (@Id, @ChatServerId, @UserId, @InvitationId, @CreatedAt, @ModifiedAt) 
               """, item, tx, cancellationToken: ct));

                await tx.CommitAsync(ct);
            }
            catch (Exception ex)
            {
                var msg = $"Error while trying to save server member {item.Id}";
                throw new Exception(msg, ex);
            }
        }
    }
}
