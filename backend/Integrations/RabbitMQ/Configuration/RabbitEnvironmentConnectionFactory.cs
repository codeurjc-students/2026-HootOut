using RabbitMQ.Client;

namespace HootOut.RabbitMQ.Configuration
{
    public class RabbitEnvironmentConnectionFactory : IRabbitConnectionFactory
    {
        private ConnectionFactory? connectionFactory;
        public ConnectionFactory GetConnectionFactory()
        {
            if (connectionFactory == null)
            {
                connectionFactory = new ConnectionFactory
                {
                    HostName = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? throw new InvalidOperationException("Environment variable RABBITMQ_HOST is null"),
                    UserName = Environment.GetEnvironmentVariable("RABBITMQ_DEFAULT_USER") ?? throw new InvalidOperationException("Environment variable RABBITMQ_DEFAULT_USER is null"),
                    Password = Environment.GetEnvironmentVariable("RABBITMQ_DEFAULT_PASS") ?? throw new InvalidOperationException("Environment variable RABBITMQ_DEFAULT_PASS is null")
                };
            }

            return connectionFactory;
        }
    }
}
