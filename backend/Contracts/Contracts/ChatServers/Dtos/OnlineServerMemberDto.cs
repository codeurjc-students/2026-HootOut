namespace HootOut.Contracts.ChatServers.Dtos
{
    public sealed record OnlineServerMembersDto
    {
        public IReadOnlyList<ServerMemberDto> Items { get; init; } = [];

        public int Total { get; init; }
    }
}
