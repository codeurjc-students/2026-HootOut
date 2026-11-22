using HootOut.Contracts.ChatConversations.Dtos;
using System;
using System.Collections.Generic;
using System.Text;

namespace HootOut.ChatConversations.Repositories
{
    public interface IDirectConversationsRepository
    {
        Task<DirectConversationDto?> GetDirectConversationByIdAsync(Guid directConversatioId, Guid userId, CancellationToken ct);
    }
}
