using FluentValidation;
using HootOut.ChatMessages.Entities;
using HootOut.ChatMessages.Mappers;
using HootOut.ChatMessages.Searchs;
using HootOut.CommonDomain.Persistence;
using HootOut.Contracts.ChatMessage.Dtos;
using HootOut.Contracts.ChatMessage.Requests;
using HootOut.Contracts.ChatMessage.Services;
using HootOut.Contracts.Common;
using HootOut.Contracts.Common.Saver;
using static System.Net.Mime.MediaTypeNames;

namespace HootOut.ChatMessages.Services
{
    public class ChatMessageService : IChatMessageService
    {

        private readonly ISaver<ChatMessage> saver;
        private readonly IChatMessageSearch search;
        private readonly IChatMessageAttachmentService attachmentService;

        public ChatMessageService(
            ISaver<ChatMessage> saver,
            IChatMessageSearch search,
            IChatMessageAttachmentService attachmentService)
        {
            this.saver = saver ?? throw new ArgumentNullException(nameof(saver));
            this.search = search ?? throw new ArgumentNullException(nameof(search));
            this.attachmentService = attachmentService ?? throw new ArgumentNullException(nameof(attachmentService));
        }
        public async Task<PageResult<ChatMessageDto>> GetMessagesByChannelAsync(Guid channelId, Guid? next, int pageSize = 10, CancellationToken ct = default)
        {
            if (Guid.Empty == channelId)
            {
                throw new ArgumentNullException(nameof(channelId)); //TO-DO proper parameter validation
            }
            var messages = await search.SearchMessagesByChannelIdAsync(channelId, next, pageSize, ct);

            foreach (var message in messages.Items)
            {
                await attachmentService.SetAttachmentsUrl(message.Attachments, ct);
            }

            return messages;
        }

        public async Task<ChatMessageDto> CreateChatMessageAsync(CreateChatMessageRequest request, CancellationToken ct = default)
        {
            // TO-DO proper parameter validation
            ArgumentNullException.ThrowIfNull(request);

            if (request.Content.Length >= 60000) throw new ValidationException(nameof(request.Content));

            ChatMessage chatMessage = Persistable.CreateNew<ChatMessage>();
            chatMessage.Content = request.Content;
            chatMessage.ChatChannelId = request.ChatChannelId;
            chatMessage.AuthorId = request.AuthorId;

            await saver.SaveAsync(chatMessage, ct);
            ChatMessageDto messageDto = chatMessage.ToDto();

            if (request != null && request.Attachments.Any())
            {
                messageDto.Attachments = (await attachmentService.CreateMessageAttachments(chatMessage.Id, request.Attachments, ct)).ToList();
            }

            return messageDto;
        }
    }
}
