using HootOut.Contracts.ChatConversations.Dtos;
using System;
using System.Collections.Generic;
using System.Text;

namespace HootOut.Contracts.ChatConversations.Services
{
    public interface IDirectConversationsService
    {
        Task<DirectConversationDto?> GetDirectConversationByIdAsync(Guid directConversationId, Guid userId, CancellationToken ct);
    }
}
