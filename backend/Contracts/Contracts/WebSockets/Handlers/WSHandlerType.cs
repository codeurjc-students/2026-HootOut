namespace HootOut.Contracts.WebSockets.Handlers
{
    public enum WSHandlerType
    {
        Invalid = 0,
        Subscribe = 1,
        Unsubscribe = 2,
        ChatMessage = 3,
    }
}