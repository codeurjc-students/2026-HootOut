using HootOut.ChatConversations.Repositories;
using HootOut.CommonDomain.FileStorage;
using HootOut.Contracts.ChatConversations.Dtos;
using HootOut.Contracts.ChatConversations.Services;

namespace HootOut.ChatConversations.Services
{
    public class DirectConversationsService : IDirectConversationsService
    {
        private readonly IDirectConversationsRepository directConversationsRepository;
        private readonly IMediaUrlService mediaUrlService;

        public DirectConversationsService(IDirectConversationsRepository directConversationsRepository, IMediaUrlService mediaUrlService)
        {
            this.directConversationsRepository = directConversationsRepository ?? throw new ArgumentNullException(nameof(directConversationsRepository));
            this.mediaUrlService = mediaUrlService ?? throw new ArgumentNullException(nameof(mediaUrlService));
        }

        public async Task<DirectConversationDto?> GetDirectConversationByIdAsync(Guid directConversationId, Guid userId, CancellationToken ct)
        {
            var conversation = await directConversationsRepository.GetDirectConversationByIdAsync(directConversationId, userId, ct);

            if (conversation?.User1?.ProfilePicture != null)
            {
                conversation.User1.ProfilePicture.Url = await mediaUrlService.GetViewUrlAsync(conversation.User1.ProfilePicture.MediaFileId, conversation.User1.ProfilePicture.ContentType, conversation.User1.ProfilePicture.FileName) ?? "";
            }
            if (conversation?.User2?.ProfilePicture != null)
            {
                conversation.User2.ProfilePicture.Url = await mediaUrlService.GetViewUrlAsync(conversation.User2.ProfilePicture.MediaFileId, conversation.User2.ProfilePicture.ContentType, conversation.User2.ProfilePicture.FileName) ?? "";
            }
            return conversation;
        }
    }
}
