using FluentAssertions;
using HootOut.CommonDomain.Persistence;
using HootOut.PostgreSQL.Repositories.ChatMessages;
using Moq;

namespace HootOut.PostgreSQL.UnitTests.Repositories.ChatMessages
{
    public class ChatMessageRepositoryTests
    {
        [Fact]
        public void Constructor_NullProvider_ThrowsArgumentNullException()
        {
            var act = () => new ChatMessageRepository(null!);

            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("provider");
        }

        [Fact]
        public void Constructor_ValidProvider_DoesNotThrow()
        {
            var provider = new Mock<IPersistenceProvider>();

            var act = () => new ChatMessageRepository(provider.Object);

            act.Should().NotThrow();
        }

        [Fact]
        public async Task SearchMessagesByChannelIdAsync_ConnectionFails_PropagatesExceptionAndPassesToken()
        {
            using var cts = new CancellationTokenSource();
            var provider = new Mock<IPersistenceProvider>(MockBehavior.Strict);
            provider
                .Setup(p => p.OpenConnectionAsync(cts.Token))
                .ThrowsAsync(new InvalidOperationException("connection failed"));

            var repository = new ChatMessageRepository(provider.Object);

            var act = () => repository.SearchMessagesByChannelIdAsync(Guid.NewGuid(), Guid.NewGuid(), 10, cts.Token);

            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("connection failed");
            provider.Verify(p => p.OpenConnectionAsync(cts.Token), Times.Once);
        }
    }
}
