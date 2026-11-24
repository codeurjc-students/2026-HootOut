using HootOut.Contracts.WebSockets.Dtos;

namespace HootOut.Contracts.WebSockets.Services
{
    public interface IPresenceTracker
    {
        /// <summary>
        /// Subscribes the connection to the presence of the user's friends, sends it the snapshot of who is
        /// online and, if it is the first connection of the user, announces it as online.
        /// </summary>
        Task ConnectedAsync(WSUserInfo user, Guid connectionId, CancellationToken ct = default);

        /// <summary>Removes the connection. If it was the last one, announces offline after the grace period.</summary>
        Task DisconnectedAsync(WSUserInfo user, Guid connectionId, CancellationToken ct = default);

        /// <summary>
        /// Subscribes the connection to the presence of the members of a server and sends it the snapshot.
        /// Throws ValidationException if the user is not a member.
        /// </summary>
        Task SubscribeServerAsync(WSUserInfo user, Guid connectionId, Guid serverId, CancellationToken ct = default);

        /// <summary>Does nothing if the connection was not subscribed.</summary>
        Task UnsubscribeServerAsync(Guid connectionId, Guid serverId, CancellationToken ct = default);
    }
}
