using HootOut.Contracts.WebSockets.Dtos;
using System.Net.WebSockets;

namespace HootOut.HootOutWebsockets.Contracts
{
    public sealed record WSSession
    {
        public WSUserInfo? User { get; init; } 
        public required WebSocket Socket { get; init; } 
    }
}
