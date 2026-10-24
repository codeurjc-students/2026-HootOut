using System;
using System.Collections.Generic;
using System.Text;

namespace HootOut.Contracts.Common
{
    public sealed record PageResult<T>
    {
        public IReadOnlyList<T> Items { get; init; }  = new List<T>();

        public Guid? Next { get; init; }

        public int PageSize { get; init; }
    }
}
