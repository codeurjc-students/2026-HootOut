using Dapper;
using HootOut.ChatServers.ChatServers;
using HootOut.CommonDomain.Persistence;
using HootOut.Contracts.Common.Saver;

namespace HootOut.PostgreSQL.Savers.ChatServers
{
    public sealed class ChatServerSaver : ISaver<ChatServer>
    {

        private readonly IPersistenceProvider provider;

        public ChatServerSaver(IPersistenceProvider provider)
        {
            this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        private const string addChatServer = """
            INSERT INTO "HootOut"."ChatServer" ("Id", "Name", "Description", "OwnerId", "CreatedAt", "ModifiedAt") 
            VALUES (@Id, @Name, @Description, @OwnerId, @CreatedAt, @ModifiedAt)
        """;

        public async Task SaveAsync(ChatServer item, CancellationToken ct = default)
        {
            await using var conn = await provider.OpenConnectionAsync(ct);
            await using var tx = await conn.BeginTransactionAsync(ct);
            try
            {
                await conn.ExecuteAsync(
                    new CommandDefinition(addChatServer, new
                    {
                        item.Id,
                        item.Name,
                        item.Description,
                        item.OwnerId,
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
