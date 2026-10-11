using HootOut.Contracts.Common.Messages.Services;
using HootOut.RabbitMQ.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Collections.Concurrent;
using System.Text;

namespace HootOut.RabbitMQ.Services
{
    public sealed class RabbitPubSubService : IQueuePubService, IAsyncDisposable
    {
        private sealed record ConsumerState(IChannel Channel, string QueueName);
        private readonly ConcurrentDictionary<string, ConsumerState> consumers = new();
        private readonly HashSet<string> declaredForPublish = new();
        private readonly SemaphoreSlim initLock = new(1, 1);
        private readonly SemaphoreSlim publishLock = new(1, 1);
        private readonly List<IChannel> _consumerChannels = new();
        private readonly IRabbitConnectionFactory connectionFactory;
        private readonly ILogger<RabbitPubSubService> logger;
        private IConnection? connection;
        private IChannel? channel;

        public RabbitPubSubService(
            ILogger<RabbitPubSubService> logger,
            IRabbitConnectionFactory connectionFactory)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        }

        // Called by the IHostedService at the start of the program. It will be consuming the queue messages and redirect them to the subscribers.
        public async Task StartConsuming<T>(string exchange, Func<string, string, T, Task> onMessage, CancellationToken ct) where T : class
        {
            if (consumers.ContainsKey(exchange))
            {
                throw new InvalidOperationException($"The exchange is already being consumed: {exchange}");
            }

            var connection = await GetConnectionAsync(ct);
            var newChannel = await connection.CreateChannelAsync(cancellationToken: ct);

            await newChannel.ExchangeDeclareAsync(exchange, ExchangeType.Direct, durable: false, autoDelete: false, cancellationToken: ct);

            var queue = await newChannel.QueueDeclareAsync(queue: string.Empty, durable: false, exclusive: true, autoDelete: true, cancellationToken: ct);

            var consumer = new AsyncEventingBasicConsumer(newChannel);
            consumer.ReceivedAsync += async (_, eventArgs) =>
            {
                try
                {
                    var body = eventArgs.Body.ToArray();
                    var messageString = Encoding.UTF8.GetString(body) ?? string.Empty;
                    T? message = JsonConvert.DeserializeObject<T>(messageString);
                    if (message != null)
                        await onMessage(eventArgs.Exchange, eventArgs.RoutingKey, message);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error processing the message from exchange {Exchange}", exchange);
                }
            };

            await newChannel.BasicConsumeAsync(queue.QueueName, autoAck: true, consumer, ct);
            consumers[exchange] = new ConsumerState(newChannel, queue.QueueName);
        }

        public async Task Publish<T>(string exchange, string rotuingKey, T @event, CancellationToken ct = default) where T : class
        {
            IChannel newChannel;
            try
            {
                newChannel = await GetChannelAsync(ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Cannot get a new channel");
                throw;
            }

            var message = JsonConvert.SerializeObject(@event);
            var byteMessage = Encoding.UTF8.GetBytes(message);

            await publishLock.WaitAsync(ct);
            try
            {
                if (declaredForPublish.Add(exchange)) // We keep a set of all the declared exchanges
                {
                    try
                    {
                        await newChannel.ExchangeDeclareAsync(exchange, ExchangeType.Direct, durable: false, autoDelete: false, cancellationToken: ct);
                    }
                    catch
                    {
                        declaredForPublish.Remove(exchange);
                    }
                }

                await newChannel.BasicPublishAsync(exchange, rotuingKey, false, new BasicProperties { Persistent = false }, byteMessage, ct);
            }
            finally { publishLock.Release(); }
        }

        public Task Subscribe(string exchange, string routingKey, CancellationToken ct = default)
        {
            var consumer = GetConsumer(exchange);
            return consumer.Channel.QueueBindAsync(consumer.QueueName, exchange, routingKey, cancellationToken: ct);
        }

        public Task UnSubscribe(string exchange, string routingKey, CancellationToken ct = default)
        {
            var consumer = GetConsumer(exchange);
            return consumer.Channel.QueueUnbindAsync(consumer.QueueName, exchange, routingKey, cancellationToken: ct);
        }

        private async Task<IConnection> GetConnectionAsync(CancellationToken ct)
        {
            if (connection != null)
            {
                return connection;
            }

            await initLock.WaitAsync(ct);
            try
            {
                return connection ??= await connectionFactory.GetConnectionFactory().CreateConnectionAsync(ct);
            }
            finally { initLock.Release(); }
        }

        private async Task<IChannel> GetChannelAsync(CancellationToken ct)
        {
            if (channel != null)
            {
                return channel;
            }

            var connection = await GetConnectionAsync(ct);

            await initLock.WaitAsync(ct);
            try
            {
                if (channel == null)
                {
                    channel = await connection.CreateChannelAsync(cancellationToken: ct);
                }
                return channel;
            }
            finally { initLock.Release(); }
        }

        private ConsumerState GetConsumer(string exchange)
             => consumers.TryGetValue(exchange, out var c)
          ? c
          : throw new InvalidOperationException($"Exchange {exchange} was not initialized by StartConsuming");

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask; //TO-DO Dispose async
        }
    }
}
