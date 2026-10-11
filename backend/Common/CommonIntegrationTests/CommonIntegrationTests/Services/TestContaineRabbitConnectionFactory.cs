using HootOut.RabbitMQ.Configuration;
using RabbitMQ.Client;
using Testcontainers.RabbitMq;
using Xunit;

namespace HootOut.CommonIntegrationTests.Services
{
    public class TestContaineRabbitConnectionFactory : IRabbitConnectionFactory, IAsyncLifetime
    {
        private RabbitMqContainer rabbitContainer { get; } = new RabbitMqBuilder("rabbitmq:4.3.6").Build();
        private ConnectionFactory? connectionFactory;
        public ConnectionFactory GetConnectionFactory()
        {
            if (connectionFactory == null)
            {
                connectionFactory = new ConnectionFactory
                {
                    Uri = new Uri(rabbitContainer.GetConnectionString())
                };
            }

            return connectionFactory;
        }

        public async ValueTask InitializeAsync()
        {
            if (rabbitContainer.State == DotNet.Testcontainers.Containers.TestcontainersStates.Undefined)
            {
                await rabbitContainer.StartAsync();
            }
        }

        public async ValueTask DisposeAsync()
        {
            await rabbitContainer.DisposeAsync();
        }
    }
}
