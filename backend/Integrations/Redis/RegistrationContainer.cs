using Autofac;
using HootOut.Contracts.Common.Presence;
using HootOut.Infraestructure.DI;
using HootOut.Redis.Configuration;
using HootOut.Redis.Services;
using StackExchange.Redis;

namespace HootOut.Redis
{
    public class RegistrationContainer : IRegistrationContainer
    {
        public void Register(ContainerBuilder builder)
        {
            builder.RegisterType<RedisEnvironmentConnectionFactory>().As<IRedisConnectionFactory>().SingleInstance();
            // One multiplexer for the whole process: it is thread safe and meant to be shared
            builder.Register(c => c.Resolve<IRedisConnectionFactory>().Connect()).As<IConnectionMultiplexer>().SingleInstance();

            builder.RegisterType<PresenceInstance>().AsSelf().SingleInstance();
            builder.RegisterInstance(new PresenceOptions()).AsSelf().SingleInstance();
            builder.RegisterType<RedisPresenceStore>().As<IPresenceStore>().SingleInstance();
        }
    }
}
