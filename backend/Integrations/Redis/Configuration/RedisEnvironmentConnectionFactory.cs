using StackExchange.Redis;

namespace HootOut.Redis.Configuration
{
    public sealed class RedisEnvironmentConnectionFactory : IRedisConnectionFactory
    {
        public IConnectionMultiplexer Connect()
        {
            var host = Environment.GetEnvironmentVariable("REDIS_HOST")
                ?? throw new InvalidOperationException("REDIS_HOST is not defined");
            var port = int.TryParse(Environment.GetEnvironmentVariable("REDIS_PORT"), out var p) ? p : 6379;

            var options = new ConfigurationOptions
            {
                Password = Environment.GetEnvironmentVariable("REDIS_PASSWORD"),
                Ssl = bool.TryParse(Environment.GetEnvironmentVariable("REDIS_SSL"), out var ssl) && ssl,
                // Keep retrying in the background instead of failing the start if Valkey is not ready yet
                AbortOnConnectFail = false,
                ClientName = "hootout",
            };
            options.EndPoints.Add(host, port);

            return ConnectionMultiplexer.Connect(options);
        }
    }
}
