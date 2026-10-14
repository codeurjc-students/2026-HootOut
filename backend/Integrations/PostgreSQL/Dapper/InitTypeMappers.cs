using Dapper;
using HootOut.CommonDomain.DefaultValues;

namespace HootOut.PostgreSQL.Dapper
{
    internal class InitTypeMappers : IDefaultValues
    {
        public async Task InitAsync(CancellationToken ct)
        {
            SqlMapper.AddTypeHandler(new DateTimeMapper());
        }

        public int Priority => 1;
    }
}
