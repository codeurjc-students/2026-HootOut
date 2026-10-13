using HootOut.Contracts.ChatServers.Dtos;
using HootOut.Contracts.ChatServers.Requests;

namespace HootOut.Contracts.ChatServers.Services
{
    public interface IChatServerService
    {
        Task<ChatServerDto> CreateChatServerAsync(CreateChatServerRequest request, Guid ownerId, CancellationToken ct = default);
        Task<IEnumerable<ChatServerDto>> GetAllChatServerDtosAsync(CancellationToken ct);
    }
}
