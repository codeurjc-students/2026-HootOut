using HootOut.ChatServers.ChatServers;
using HootOut.ChatServers.Mappers;
using HootOut.ChatServers.Searchs;
using HootOut.Contracts.ChatServers.Dtos;
using HootOut.Contracts.ChatServers.Requests;
using HootOut.Contracts.ChatServers.Services;
using HootOut.Contracts.Common.Saver;

namespace HootOut.ChatServers.Services
{
    public sealed class ChatServerService : IChatServerService
    {
        private readonly ISaver<ChatServer> saver;
        private readonly IChatServerSearch search;

        public ChatServerService(ISaver<ChatServer> saver, IChatServerSearch search)
        {
            this.saver = saver ?? throw new ArgumentNullException(nameof(saver));
            this.search = search ?? throw new ArgumentNullException(nameof(search));
        }

        public async Task<ChatServerDto> CreateChatServerAsync(CreateChatServerRequest request, Guid ownerId, CancellationToken ct = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            request.Validate();

            var newServer = Persistable.CreateNew<ChatServer>();

            newServer.Name = request.Name;
            newServer.Description = request.Description;
            newServer.OwnerId = ownerId;

            await saver.SaveAsync(newServer);

            return newServer.ToDto();
        }

        public async Task<IEnumerable<ChatServerDto>> GetAllChatServerDtosAsync(CancellationToken ct = default)
        {
            return (await search.GetAllChatServersAsync(ct))
                .Select(x => x.ToDto())
                .ToList();
        }
    }
}
