using FluentValidation;
using FluentValidation.Results;
using HootOut.ChatMessages.Entities;
using HootOut.ChatMessages.Mappers;
using HootOut.ChatMessages.Repositories;
using HootOut.Contracts.ChatConversations.Enums;
using HootOut.Contracts.ChatConversations.Services;
using HootOut.Contracts.ChatMessage.Dtos;
using HootOut.Contracts.ChatMessage.Requests;
using HootOut.Contracts.ChatMessage.Services;
using HootOut.Contracts.ChatServers.Services;
using HootOut.Contracts.Common;
using HootOut.Contracts.Common.Saver;
using HootOut.Contracts.Friends.Services;

namespace HootOut.ChatMessages.Services
{
    public class ChatMessageService : IChatMessageService
    {

        private readonly ISaver<ChatMessage> saver;
        private readonly IChatMessageRepository search;
        private readonly IChatMessageAttachmentService attachmentService;
        private readonly IChatServerService chatServerService;
        private readonly IFriendsService friendsService;
        private readonly IChatConversationsService chatConversationsService;

        public ChatMessageService(
            ISaver<ChatMessage> saver,
            IChatMessageRepository search,
            IChatMessageAttachmentService attachmentService,
            IChatServerService chatServerService,
            IFriendsService friendsService,
            IChatConversationsService chatConversationsService
            )
        {
            this.saver = saver ?? throw new ArgumentNullException(nameof(saver));
            this.search = search ?? throw new ArgumentNullException(nameof(search));
            this.attachmentService = attachmentService ?? throw new ArgumentNullException(nameof(attachmentService));
            this.chatServerService = chatServerService ?? throw new ArgumentNullException(nameof(chatServerService));
            this.friendsService = friendsService ?? throw new ArgumentNullException(nameof(friendsService));
            this.chatConversationsService = chatConversationsService ?? throw new ArgumentNullException(nameof(chatConversationsService));
        }
        public async Task<PageResult<ChatMessageDto>> GetMessagesByConversationIdAsync(Guid conversationId, Guid? next, int pageSize = 10, CancellationToken ct = default)
        {
            if (Guid.Empty == conversationId)
            {
                throw new ArgumentNullException(nameof(conversationId)); //TO-DO proper parameter validation
            }
            var messages = await search.SearchMessagesByConversationIdAsync(conversationId, next, pageSize, ct);

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
            request.Validate();

            await ValidateUserCanAccessConversationAsync(request.ChatConversationId, request.AuthorId, ct);
            await attachmentService.ValidateAttachmentsAsync(request.AuthorId, request.Attachments, ct);

            ChatMessage chatMessage = Persistable.CreateNew<ChatMessage>();
            chatMessage.Content = request.Content;
            chatMessage.ChatConversationId = request.ChatConversationId;
            chatMessage.AuthorId = request.AuthorId;

            await saver.SaveAsync(chatMessage, ct);
            ChatMessageDto messageDto = chatMessage.ToDto();

            if (request != null && request.Attachments.Any())
            {
                messageDto.Attachments = (await attachmentService.CreateMessageAttachments(chatMessage.Id, request.Attachments, ct)).ToList();
            }

            return messageDto;
        }

        /// <summary>
        /// Throws a <see cref="ValidationException"/> with <see cref="CannotAccessConversationMessage"/> if the user
        /// cannot read or write in the conversation: it does not exist, it is a direct conversation and they are not
        /// friends, or it is a channel and the user is not a member of its server.
        /// Used before saving a message and before subscribing to a conversation through WebSockets.
        /// </summary>
        public async Task ValidateUserCanAccessConversationAsync(Guid conversationId, Guid userId, CancellationToken ct = default)
        {
            var conversation = await chatConversationsService.GetChatConversationByIdAsync(conversationId, ct);
            if (conversation == null)
            {
                throw new ValidationException(new[] { new ValidationFailure("Conversation", "Cannot send a message to this conversation", conversationId) });
            }

            try
            {
                switch (conversation.ConversationType)
                {
                    case ChatConversationType.Channel:
                        await chatServerService.ValidateUserCanSendMessageAsync(conversationId, userId, ct);
                        break;
                    case ChatConversationType.Direct:
                        await friendsService.ValidateUserCanSendMessageAsync(conversationId, userId, ct);
                        break;
                    default:
                        throw new ValidationException(new[] { new ValidationFailure("Conversation", "Invalid conversation type") });
                }
            }
            catch (ValidationException)
            {
                throw new ValidationException(new[] { new ValidationFailure("Conversation", "Cannot send a message to this conversation", conversationId) });
            }
        }
    }
}
