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

        private static Guid serverId = Guid.CreateVersion7();

        public ChatChannelsService(ISaver<ChatChannel> saver,
            IChatChannelSearch search)
        {
            this.saver = saver ?? throw new ArgumentNullException(nameof(saver));
            this.search = search ?? throw new ArgumentNullException(nameof(search));
        }

        public ChatChannelDto CreateChatChannel(CreateChatChannelRequest request)
        {
            if (string.IsNullOrEmpty(request.Name))
            {
                throw new ArgumentException(nameof(request.Name)); // TO-DO Proper validation
            }

            ChatChannel newChannel = Persistable.CreateNew<ChatChannel>();
            newChannel.ChatServerId = serverId; //TO-DO all share the same Server because there are no servers yet.
            newChannel.Name = request.Name;

            this.saver.Save(newChannel);

            return newChannel.ToDto();
        }

        public IEnumerable<ChatChannelDto> GetAllChatChannelsDtos()
        {
            return search.getAllChannels()
                .Select(x => x.ToDto())
                .ToList();
        }
    }
}
