using Dapper;
using HootOut.CommonDomain.DefaultValues;

namespace HootOut.PostgreSQL.Dapper
{
    internal class InitTypeMappers : IDefaultValues
    {
        public void Init()
        {
            SqlMapper.AddTypeHandler(new DateTimeMapper());
        }

        public int Priority => 1;
    }
}
