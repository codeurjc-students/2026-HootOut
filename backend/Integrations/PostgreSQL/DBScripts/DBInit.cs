using Dapper;
using HootOut.CommonDomain.DefaultValues;
using HootOut.CommonDomain.Persistence;
using HootOut.PostgreSQL.Properties;

namespace HootOut.PostgreSQL.DBScripts
{
    public class DBInit : IDefaultValues
    {
        private IPersistenceProvider sqlProvider;

        public DBInit(IPersistenceProvider sqlProvider)
        {
            this.sqlProvider = sqlProvider ?? throw new ArgumentNullException(nameof(sqlProvider));
        }

        public void Init()
        {
            using (var conn = sqlProvider.GetNewConnection())
            {
                conn.Execute(Resources.UserInfoTable);
            }
        }

        public int Priority => 0;
    }
}
