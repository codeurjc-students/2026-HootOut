using HootOut.ChatMessages.ChatMessages;
using HootOut.ChatMessages.Mappers;
using HootOut.ChatMessages.Searchs;
using HootOut.Contracts.ChatMessage.Dtos;
using HootOut.Contracts.ChatMessage.Requests;
using HootOut.Contracts.ChatMessage.Services;
using HootOut.Contracts.Common.Saver;

namespace HootOut.ChatMessages.Services
{
    public class ChatMessageService : IChatMessageService
    {

        private ISaver<ChatMessage> saver;
        private IChatMessageSearch search;

        public ChatMessageService(
            ISaver<ChatMessage> saver,
            IChatMessageSearch search)
        {
            this.saver = saver ?? throw new ArgumentNullException(nameof(saver));
            this.search = search ?? throw new ArgumentNullException(nameof(search));
        }

        public IEnumerable<ChatMessageDto> GetMessagesByChannel(Guid channelId)
        {
            if (Guid.Empty == channelId)
            {
                throw new ArgumentNullException(nameof(channelId)); //TO-DO proper parameter validation
            }
            return search.GetMessagesByChannelId(channelId);
        }

        public ChatMessageDto CreateChatMessage(CreateChatMessageRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request)); // TO-DO proper parameter validation
            if (request.Content.Length >= 60000) throw new ArgumentOutOfRangeException(nameof(request.Content));

            ChatMessage chatMessage = Persistable.CreateNew<ChatMessage>();
            chatMessage.Content = request.Content;
            chatMessage.ChatChannelId = request.ChatChannelId;
            chatMessage.AuthorId = request.AuthorId;

            this.saver.Save(chatMessage);

            ChatMessageDto messageDto = chatMessage.ToDto();

            return messageDto;
        }
    }
}
