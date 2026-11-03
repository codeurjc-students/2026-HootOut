namespace HootOut.Contracts.Common
{
    public sealed record PageRequest
    {
        public Guid? Next { get; set; } = null;
        public string? SortBy { get; set; } = null;
        public bool Descending { get; set; } = true;
        public int PageSize { get; set; } = 10;
        public int Size => Math.Clamp(PageSize, 1, 50);
    }
}
