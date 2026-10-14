using HootOut.ChatChannels.ChatChannels;
using HootOut.ChatChannels.Mappers;
using HootOut.ChatChannels.Search;
using HootOut.Contracts.ChatChannels.Dtos;
using HootOut.Contracts.ChatChannels.Requests;
using HootOut.Contracts.ChatChannels.Services;
using HootOut.Contracts.Common.Saver;

namespace HootOut.ChatChannels.Services
{
    public class ChatChannelsService : IChatChannelsService
    {
        private readonly ISaver<ChatChannel> saver;
        private readonly IChatChannelSearch search;

        public ChatChannelsService(ISaver<ChatChannel> saver,
            IChatChannelSearch search)
        {
            this.saver = saver ?? throw new ArgumentNullException(nameof(saver));
            this.search = search ?? throw new ArgumentNullException(nameof(search));
        }

        public async Task<ChatChannelDto> CreateChatChannelAsync(CreateChatChannelRequest request, CancellationToken ct = default)
        {
            request.Validate();

            ChatChannel newChannel = Persistable.CreateNew<ChatChannel>();
            newChannel.ChatServerId = request.ChatServerId;
            newChannel.Name = request.Name;

            await saver.SaveAsync(newChannel, ct);

            return newChannel.ToDto();
        }

        public async Task<IEnumerable<ChatChannelDto>> GetAllChatChannelsDtosAsync(CancellationToken ct = default)
        {
            return [.. (await search.GetAllChannelsAsync(ct)).Select(x => x.ToDto())];
        }

        public async Task<IEnumerable<ChatChannelDto>> GetChannelsByServerIdAsync(Guid serverId, CancellationToken ct = default)
        {
            return [.. (await search.GetChannelsByServerId(serverId, ct)).Select(x => x.ToDto())];
        }
    }
}
