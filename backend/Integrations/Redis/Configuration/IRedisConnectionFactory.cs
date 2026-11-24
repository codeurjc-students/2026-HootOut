using StackExchange.Redis;

namespace HootOut.Redis.Configuration
{
    public interface IRedisConnectionFactory
    {
        IConnectionMultiplexer Connect();
    }
}
