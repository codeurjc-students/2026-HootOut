using RabbitMQ.Client;

namespace HootOut.RabbitMQ.Configuration
{
    public interface IRabbitConnectionFactory
    {
        public ConnectionFactory GetConnectionFactory();
    }
}
