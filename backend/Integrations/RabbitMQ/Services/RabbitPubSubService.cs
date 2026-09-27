using HootOut.Contracts.Common.Messages.Events;
using HootOut.Contracts.Common.Messages.Services;
using HootOut.RabbitMQ.Configuration;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;

namespace HootOut.RabbitMQ.Services
{
    public class RabbitPubSubService : IPubSubService
    {
        private IRabbitConnectionFactory connectionFactory;  

        private IConnection? connection;

        private IChannel? channel;

        private IEnumerable<AsyncEventingBasicConsumer> consumers = new List<AsyncEventingBasicConsumer>();
        
        public RabbitPubSubService(IRabbitConnectionFactory connectionFactory)
        {
            this.connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        }

        public async Task Publish<T>(string queue, T @event) where T : MessageEvent
        {
            connection ??= await connectionFactory.GetConnectionFactory().CreateConnectionAsync();

            channel ??= await connection!.CreateChannelAsync();

                await QueueDeclare(channel, queue);

                var message = JsonConvert.SerializeObject(@event);
                var byteMessage = Encoding.UTF8.GetBytes(message);

                await channel.BasicPublishAsync(
                    exchange: string.Empty,
                    routingKey: queue,
                    mandatory: true,
                    basicProperties: new BasicProperties { Persistent = false },
                    body: byteMessage
                    ); 
        }
        public async Task Subscribe<T>(string queue, Action<T> onMessagReceived) where T : MessageEvent
        {
            connection ??= await connectionFactory.GetConnectionFactory().CreateConnectionAsync();

            channel ??= await connection!.CreateChannelAsync();

            await QueueDeclare(channel, queue);

            var consumer = new AsyncEventingBasicConsumer(channel);

            consumer.ReceivedAsync += async (sender, eventArgs) =>
            {
                var body = eventArgs.Body.ToArray();
                var messageString = Encoding.UTF8.GetString(body) ?? string.Empty;
                T? message = JsonConvert.DeserializeObject<T>(messageString);
                onMessagReceived.Invoke(message!);
            };

            await channel.BasicConsumeAsync(queue, autoAck: false, consumer); 
        }

        private async Task QueueDeclare(IChannel channel, string queue)
        {
            await channel!.QueueDeclareAsync(
               queue: queue,
               durable: true,
               exclusive: false,
               autoDelete: false,
               arguments: null);
        }
    }
}
