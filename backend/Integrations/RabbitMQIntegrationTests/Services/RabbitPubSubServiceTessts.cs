//using Autofac;
//using FluentAssertions;
//using HootOut.CommonIntegrationTests.Services;
//using HootOut.Contracts.Common.Messages.Events;
//using HootOut.Contracts.Common.Messages.Services;
//using HootOut.Infraestructure.DI;
//using HootOut.RabbitMQ.Configuration;
//using RabbitMQ.Client;
//using Testcontainers.RabbitMq;

//namespace HootOut.RabbitMQIntegrationTests.Services
//{
//    public class RabbitPubSubServiceTests : IAsyncLifetime
//    {
//        private IContainer container;

//        private IQueuePubService pubSubService; 



//        public async ValueTask InitializeAsync()
//        {

//                .ConfigureAwait(false);
//            TestContaineRabbitConnectionFactory rabbitFactory = new TestContaineRabbitConnectionFactory();
//            rabbitFactory.ConnectionString = rabbitContainer.GetConnectionString();
//        }

//        public ValueTask DisposeAsync()
//        {
//            return rabbitContainer.DisposeAsync();
//        }

//        public RabbitPubSubServiceTests()
//        {
//            var builder = new ContainerBuilder();
//            new RegistrationManager().RegisterAllAssemblies(builder);
//            builder.RegisterInstance(rabbitFactory).As<IRabbitConnectionFactory>();

//            container = builder.Build();

//            pubSubService = container.Resolve<IQueuePubService>();
//        }

//        [Fact]
//        public async Task PublishMessage()
//        {
//            var sentMessage = new MessageEvent { Queue = "test" };

//            var tcs = new TaskCompletionSource<MessageEvent>();

//            await pubSubService.Subscribe<MessageEvent>(sentMessage.Queue, (message) =>
//            {
//                tcs.SetResult(message);
//            });

//            await Task.Run(async () =>
//            {
//                var publisherService = container.Resolve<IQueuePubService>();
//                await publisherService.Publish(sentMessage.Queue, sentMessage);
//            }, TestContext.Current.CancellationToken);

//            var receivedMessage = await tcs.Task;

//            receivedMessage.Should().NotBeNull();
//            receivedMessage.Should().NotBeSameAs(sentMessage);
//            receivedMessage.ID.Should().Be(sentMessage.ID);
//            receivedMessage.Queue.Should().Be(sentMessage.Queue);
//            receivedMessage.CreatedDate.Should().Be(sentMessage.CreatedDate);
//        }
//    } 
//}
