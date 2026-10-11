using Autofac;
using FluentAssertions;
using HootOut.CommonIntegrationTests.Services;
using HootOut.Contracts.Common.Messages.Events;
using HootOut.Contracts.Common.Messages.Services;
using HootOut.Contracts.Common.Saver;
using HootOut.Contracts.WebSockets.Dtos;
using HootOut.Contracts.WebSockets.Entities;
using HootOut.Contracts.WebSockets.Handlers;
using HootOut.Infraestructure.DI;
using HootOut.RabbitMQ.Configuration;
using HootOut.RabbitMQ.Services;
using Microsoft.Extensions.Logging;
using Moq;
using RabbitMQ.Client;
using Testcontainers.RabbitMq;

namespace HootOut.RabbitMQIntegrationTests.Services
{
    public class RabbitPubSubServiceTests : IAsyncLifetime
    {
        private IQueuePubService pubSubService;
        private TestContaineRabbitConnectionFactory rabbitConnectionFactory { get; set; }

        public RabbitPubSubServiceTests()
        {
            rabbitConnectionFactory = new TestContaineRabbitConnectionFactory();
            pubSubService = new RabbitPubSubService(new Mock<ILogger<RabbitPubSubService>>().Object, rabbitConnectionFactory);
        }

        public async ValueTask InitializeAsync()
        {
            await rabbitConnectionFactory.InitializeAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await rabbitConnectionFactory.DisposeAsync();
        } 

        [Fact]
        public async Task RabbitPubSubService_PublishSubscribe_OneSubscriber()
        {
            var ct = TestContext.Current.CancellationToken;

            var tcs = new TaskCompletionSource<WebSocketMessage>();
            string exchange = "testexchange";
            var testMessage = new WebSocketMessageDto { Channel = "test", Payload = "testPayload" };

            await pubSubService.StartConsuming<WebSocketMessage>(exchange, 
                async (exchange, channelId, message) => { 
                    tcs.SetResult(message);
                    await Task.CompletedTask;
                    },
                ct);

            await pubSubService.Subscribe(exchange, testMessage.Channel, ct);
            await pubSubService.Publish(exchange, testMessage.Channel, testMessage, ct);

            var receivedMessage = await tcs.Task;

            receivedMessage.Should().NotBeNull();
            receivedMessage.Should().NotBeSameAs(testMessage);
            receivedMessage.Id.Should().Be(testMessage.Id);
            receivedMessage.Channel.Should().Be(testMessage.Channel);
            receivedMessage.Payload.Should().Be(testMessage.Payload);
        }

        [Fact]
        public async Task RabbitPubSubService_PublishSubscribe_ThreeSubscribers()
        {
            var ct = TestContext.Current.CancellationToken;

            var tcs = new TaskCompletionSource<WebSocketMessage>();
            var tcs2 = new TaskCompletionSource<WebSocketMessage>();
            var tcs3 = new TaskCompletionSource<WebSocketMessage>();

            string exchange = "testexchange";
            var testMessage = new WebSocketMessageDto { Channel = "test", Payload = "testPayload" };

            var pubSubService2 = new RabbitPubSubService(new Mock<ILogger<RabbitPubSubService>>().Object, rabbitConnectionFactory);
            var pubSubService3 = new RabbitPubSubService(new Mock<ILogger<RabbitPubSubService>>().Object, rabbitConnectionFactory);

            await pubSubService.StartConsuming<WebSocketMessage>(exchange,
                async (exchange, channelId, message) => {
                    tcs.SetResult(message);
                    await Task.CompletedTask;
                },
                ct);

            await pubSubService2.StartConsuming<WebSocketMessage>(exchange,
                async (exchange, channelId, message) => {
                    tcs2.SetResult(message);
                    await Task.CompletedTask;
                },
                ct);

            await pubSubService3.StartConsuming<WebSocketMessage>(exchange,
                async (exchange, channelId, message) => {
                    tcs3.SetResult(message);
                    await Task.CompletedTask;
                },
                ct);

            await pubSubService.Subscribe(exchange, testMessage.Channel, ct);
            await pubSubService2.Subscribe(exchange, testMessage.Channel, ct);
            await pubSubService3.Subscribe(exchange, testMessage.Channel, ct);

            await pubSubService.Publish(exchange, testMessage.Channel, testMessage, ct);

            WebSocketMessage[] receivedMessages = new WebSocketMessage[3];

            receivedMessages[0] = await tcs.Task;
            receivedMessages[1] = await tcs2.Task;
            receivedMessages[2] = await tcs3.Task;

            foreach (var message in receivedMessages)
            {
                message.Should().NotBeNull();
                message.Should().NotBeSameAs(testMessage);
                message.Id.Should().Be(testMessage.Id);
                message.Channel.Should().Be(testMessage.Channel);
                message.Payload.Should().Be(testMessage.Payload);
            } 
        }
    }
}
