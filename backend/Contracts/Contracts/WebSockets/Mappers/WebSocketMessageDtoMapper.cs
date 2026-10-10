using HootOut.Contracts.WebSockets.Dtos;
using HootOut.Contracts.WebSockets.Entities;

namespace HootOut.Contracts.WebSockets.Mappers
{
    public static class WebSocketMessageDtoMapper
    {
        public static WebSocketMessageDto ToDto(this WebSocketMessage message)
        {
            return new WebSocketMessageDto
            {
                Id = message.Id,
                ReplyTo = message.ReplyTo,
                Channel = message.Channel,
                Type = message.Type,
                Payload = message.Payload,
                Error = message.Error
            };
        }
    }
}
