using HootOut.ChatChannels.ChatChannels;
using HootOut.Contracts.ChatChannels.Dtos;
using System;
using System.Collections.Generic;
using System.Text;

namespace HootOut.ChatChannels.Mappers
{
    public static class ChatChannelDtoMapper
    {
        public static ChatChannelDto ToDto(this ChatChannel chatChannel)
        {
            return new ChatChannelDto
            {
                Id = chatChannel.Id,
                Name = chatChannel.Name,
                ChatServerId = chatChannel.ChatServerId,
                CreatedAt = chatChannel.CreatedAt,
                ModifiedAt = chatChannel.ModifiedAt,
            };
        }
    }
}
