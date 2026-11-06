using FluentValidation;
using FluentValidation.Results;
using HootOut.ChatChannels.Entities;
using HootOut.ChatChannels.Mappers;
using HootOut.ChatChannels.Repositories;
using HootOut.Contracts.ChatChannels.Dtos;
using HootOut.Contracts.ChatChannels.Requests;
using HootOut.Contracts.ChatChannels.Services;
using HootOut.Contracts.Common.Saver;

namespace HootOut.ChatChannels.Services
{
    public class ChatChannelsService : IChatChannelsService
    {
        private readonly ISaver<ChatChannel> saver;
        private readonly IChatChannelRepository search;

        public ChatChannelsService(ISaver<ChatChannel> saver,
            IChatChannelRepository search)
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

        public async Task<IEnumerable<ChatChannelDto>> GetChannelsByServerIdAsync(Guid serverId, CancellationToken ct = default)
        {
            return (await search.GetChannelsByServerId(serverId, ct)).Select(x => x.ToDto());
        }

        public async Task<ChatChannelDto> UpdateChatChannelAsync(UpdateChatChannelRequest request, CancellationToken ct = default)
        {
            request.Validate();

            ChatChannel? channel = await search.GetChannelByIdAsync(request.Id, ct);
            if (channel == null) throw new FluentValidation.ValidationException("Channel not found"); //TO-DO Check user is on server

            channel.Name = request.Name;
            channel.ModifiedAt = DateTime.UtcNow;
            await saver.SaveAsync(channel, ct);

            return channel.ToDto();
        }

        public async Task<Guid> DeleteChatChannelAsync(Guid channelId, CancellationToken ct = default)
        {
            ChatChannel? channel = await search.GetChannelByIdAsync(channelId, ct); // TO-DO Check 
            if (channel == null)
            {
                throw new ValidationException(new[] { new ValidationFailure("channel", "Channel not found") });
            }

            await saver.DeleteAsync(channel, ct);

            return channel.Id;
        }
    }
}
