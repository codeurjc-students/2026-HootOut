using Microsoft.AspNetCore.Mvc;

namespace HootOut.Contracts.Common
{
    public sealed record PageRequest
    {
        [FromQuery]
        public Guid? Next { get; set; } = null;
        [FromQuery]
        public string? SortBy { get; set; } = null;
        [FromQuery]
        public bool Descending { get; set; } = true;
        [FromQuery]
        public int PageSize { get; set; } = 10;
        public int Size => Math.Clamp(PageSize, 1, 50);
    }
}
