using FluentAssertions;
using HootOut.ChatChannels.Entities;
using HootOut.CommonDomain.Persistence;
using HootOut.Contracts.Common.Saver;
using HootOut.PostgreSQL.Savers.ChatChannels;
using Moq;

namespace HootOut.PostgreSQL.UnitTests.Savers.ChatChannels
{
    public class ChatChannelsSaverTests
    {
        public static TheoryData<string> SaverMethods =>
        [
            nameof(ChatChannelsSaver.SaveAsync),
            nameof(ChatChannelsSaver.DeleteAsync)
        ];

        [Fact]
        public void Constructor_NullProvider_ThrowsArgumentNullException()
        {
            var act = () => new ChatChannelsSaver(null!);

            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("provider");
        }

        [Fact]
        public void Constructor_ValidProvider_DoesNotThrow()
        {
            var provider = new Mock<IPersistenceProvider>();

            var act = () => new ChatChannelsSaver(provider.Object);

            act.Should().NotThrow();
        }

        [Theory]
        [MemberData(nameof(SaverMethods))]
        public async Task AnyMethod_ConnectionFails_PropagatesProviderExceptionAndPassesToken(string method)
        {
            using var cts = new CancellationTokenSource();
            var provider = new Mock<IPersistenceProvider>(MockBehavior.Strict);
            provider
                .Setup(p => p.OpenConnectionAsync(cts.Token))
                .ThrowsAsync(new InvalidOperationException("connection failed"));

            // Called through the interface to make sure the class methods replace the default interface implementations
            ISaver<ChatChannel> saver = new ChatChannelsSaver(provider.Object);
            var channel = Persistable.CreateNew<ChatChannel>();

            Func<Task> act = method switch
            {
                nameof(ChatChannelsSaver.SaveAsync) => () => saver.SaveAsync(channel, cts.Token),
                nameof(ChatChannelsSaver.DeleteAsync) => () => saver.DeleteAsync(channel, cts.Token),
                _ => throw new ArgumentOutOfRangeException(nameof(method), method, null)
            };

            // Opening the connection happens outside the try/catch, so the exception is not wrapped
            await act.Should().ThrowExactlyAsync<InvalidOperationException>()
                .WithMessage("connection failed");
            provider.Verify(p => p.OpenConnectionAsync(cts.Token), Times.Once);
        }
    }
}
