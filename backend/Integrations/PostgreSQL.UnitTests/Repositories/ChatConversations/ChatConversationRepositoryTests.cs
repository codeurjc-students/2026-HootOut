using FluentAssertions;
using HootOut.CommonDomain.Persistence;
using HootOut.PostgreSQL.Repositories.ChatConversation;
using Moq;

namespace HootOut.PostgreSQL.UnitTests.Repositories.ChatConversations
{
    public class ChatConversationRepositoryTests
    {
        [Fact]
        public void Constructor_NullProvider_ThrowsArgumentNullException()
        {
            var act = () => new ChatConversationRepository(null!);

            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("provider");
        }

        [Fact]
        public void Constructor_ValidProvider_DoesNotThrow()
        {
            var provider = new Mock<IPersistenceProvider>();

            var act = () => new ChatConversationRepository(provider.Object);

            act.Should().NotThrow();
        }

        [Fact]
        public async Task GetChatConversationByIdAsync_ConnectionFails_PropagatesProviderExceptionAndPassesToken()
        {
            using var cts = new CancellationTokenSource();
            var provider = new Mock<IPersistenceProvider>(MockBehavior.Strict);
            provider
                .Setup(p => p.OpenConnectionAsync(cts.Token))
                .ThrowsAsync(new InvalidOperationException("connection failed"));

            var repository = new ChatConversationRepository(provider.Object);

            var act = () => repository.GetChatConversationByIdAsync(Guid.NewGuid(), cts.Token);

            await act.Should().ThrowExactlyAsync<InvalidOperationException>()
                .WithMessage("connection failed");
            provider.Verify(p => p.OpenConnectionAsync(cts.Token), Times.Once);
        }
    }
}
