using HootOut.Contracts.Common;

namespace HootOut.PostgreSQL.Helpers
{
    public sealed record KeysetSort(string SortExpr, string CursorSortExpr, string? CursorJoin = null);

    public static class PaginatedQueryHelper
    {
        public static string CursorFilter(string fromJoins, string sortExpr, PageRequest req)
        {
            return req.Next is null ? "" : $""" 
            AND({sortExpr}, m."Id") {(req.Descending ? "<" : ">")}
            (
                SELECT {sortExpr}, m."Id"
                {fromJoins}
            WHERE m."Id" = @Next)
            """;
        }
        public static string OrderBy(string sortExpr, PageRequest req)
        {
            var dir = req.Descending ? "DESC" : "ASC";
            return $"""ORDER BY {sortExpr} {dir}, m."Id" {dir}""";
        }

        public static PageResult<T> ToPage<T>(List<T> rows, PageRequest req, Func<T, Guid> id, int? total = null)
        {
            var hasMore = rows.Count > req.Size;
            if (hasMore) rows.RemoveAt(rows.Count - 1);
            return new PageResult<T> { Items = rows, Next = hasMore ? id(rows[^1]) : null, Total = total, PageSize = req.Size };
        }
    }
}
