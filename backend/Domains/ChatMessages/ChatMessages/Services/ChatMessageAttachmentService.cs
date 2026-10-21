using HootOut.ChatMessages.Entities;
using HootOut.ChatMessages.Searchs;
using HootOut.CommonDomain.FileStorage;
using HootOut.Contracts.ChatMessage.Dtos;
using HootOut.Contracts.ChatMessage.Requests;
using HootOut.Contracts.ChatMessage.Services;
using HootOut.Contracts.Common.Saver;

namespace HootOut.ChatMessages.Services
{
    public class ChatMessageAttachmentService : IChatMessageAttachmentService
    {
        private readonly ISaver<ChatMessageAttachment> saver;
        private readonly IMediaUrlService mediaUrlService;
        private readonly IChatMessageAttachmentSearch chatMessageAttachmentSearch;

        public ChatMessageAttachmentService(ISaver<ChatMessageAttachment> saver, IMediaUrlService mediaUrlService, IChatMessageAttachmentSearch chatMessageAttachmentSearch)
        {
            this.saver = saver ?? throw new ArgumentNullException(nameof(saver));
            this.mediaUrlService = mediaUrlService ?? throw new ArgumentNullException(nameof(mediaUrlService));
            this.chatMessageAttachmentSearch = chatMessageAttachmentSearch ?? throw new ArgumentNullException(nameof(chatMessageAttachmentSearch));
        }

        public async Task<IEnumerable<ChatMessageAttachmentDto>> CreateMessageAttachments(Guid messageId, IEnumerable<CreateChatAttachmentRequest> request, CancellationToken ct = default)
        {
            if (request == null || !request.Any())
                return [];

            IEnumerable<ChatMessageAttachment> attachments = request.Select(x =>
            {
                var newAttachment = Persistable.CreateNew<ChatMessageAttachment>();
                newAttachment.MessageId = messageId;
                newAttachment.MediaFileId = x.MediaFileId;
                return newAttachment;
            }).ToList();

            await saver.SaveManyAsync(attachments, ct);
            return await GetMessageAttachmentDtos(messageId, ct);
        }

        public async Task<IEnumerable<ChatMessageAttachmentDto>> GetMessageAttachmentDtos(Guid messageId, CancellationToken ct = default)
        {
            var result = await chatMessageAttachmentSearch.GetChatMessageAttachmentDtos(messageId, ct);

            foreach (var attachment in result)
            {
                attachment.Url = await mediaUrlService.GetDownloadUrlAsync(attachment.MediaFileId, attachment.ContentType) ?? "";
            }

            return result;
        }
    }
}
