using Autofac;
using HootOut.CommonDomain.DefaultValues;
using HootOut.CommonDomain.Persistence;
using HootOut.Contracts.Common.Saver;
using HootOut.Contracts.Users.Search;
using HootOut.Infraestructure.DI;
using HootOut.PostgreSQL.Dapper;
using HootOut.PostgreSQL.DBScripts;
using HootOut.PostgreSQL.Searchs.Users;
using HootOut.PostgreSQL.Services;
using System.Reflection;

namespace HootOut.PostgreSQL
{
    public class RegistrationContainer : IRegistrationContainer
    {
        public void Register(ContainerBuilder builder)
        {
            builder.RegisterType<DBInit>().As<IDefaultValues>().SingleInstance();
            builder.RegisterType<InitTypeMappers>().As<IDefaultValues>().SingleInstance();
            builder.RegisterType<PostgreSQLProvider>().As<IPersistenceProvider>().SingleInstance();

            RegisterSavers(builder);
            RegisterSearchs(builder);
        }

        private void RegisterSavers(ContainerBuilder builder)
        {
            var assembly = Assembly.GetExecutingAssembly(); ;
            builder.RegisterAssemblyTypes(assembly).AsClosedTypesOf(typeof(ISaver<>)).SingleInstance();
        }
        private void RegisterSearchs(ContainerBuilder builder)
        {
            builder.RegisterType<UserSearch>().As<IUserSearch>().SingleInstance();
        }
    }
}
