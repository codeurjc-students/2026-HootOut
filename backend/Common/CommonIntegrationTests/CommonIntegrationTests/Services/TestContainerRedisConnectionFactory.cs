using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using HootOut.Redis.Configuration;
using StackExchange.Redis;
using Xunit;

namespace HootOut.CommonIntegrationTests.Services
{
    /// <summary>Valkey container for the tests. Same idea as TestContaineRabbitConnectionFactory.</summary>
    public class TestContainerRedisConnectionFactory : IRedisConnectionFactory, IAsyncLifetime
    {
        public const int Port = 6379;

        private readonly IContainer valkeyContainer = new ContainerBuilder("valkey/valkey:8-alpine")
            .WithPortBinding(Port, true)
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilInternalTcpPortIsAvailable(Port)
                .UntilMessageIsLogged("Ready to accept connections"))
            .Build();

        private readonly Lock connectLock = new();
        private ConnectionMultiplexer? multiplexer;

        /// <summary>Always the same multiplexer: it is shared like in the application (singleton).</summary>
        public IConnectionMultiplexer Connect()
        {
            lock (connectLock)
            {
                // allowAdmin: needed by FlushAsync
                return multiplexer ??= ConnectionMultiplexer.Connect(
                    $"{valkeyContainer.Hostname}:{valkeyContainer.GetMappedPublicPort(Port)},allowAdmin=true");
            }
        }

        /// <summary>Removes every key.</summary>
        public async Task FlushAsync()
        {
            var connection = Connect();
            foreach (var endpoint in connection.GetEndPoints())
            {
                await connection.GetServer(endpoint).FlushDatabaseAsync();
            }
        }

        public async ValueTask InitializeAsync()
        {
            if (valkeyContainer.State == TestcontainersStates.Undefined)
            {
                await valkeyContainer.StartAsync();
            }
        }

        public async ValueTask DisposeAsync()
        {
            multiplexer?.Dispose();
            await valkeyContainer.DisposeAsync();
            GC.SuppressFinalize(this);
        }
    }
}
