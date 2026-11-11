using FluentAssertions;
using HootOut.CommonDomain.Persistence;
using HootOut.PostgreSQL.Repositories.ChatMessages;
using Moq;

namespace HootOut.PostgreSQL.UnitTests.Repositories.ChatMessages
{
    public class ChatMessageAttachmentRepositoryTests
    {
        [Fact]
        public void Constructor_NullProvider_ThrowsArgumentNullException()
        {
            var act = () => new ChatMessageAttachmentRepository(null!);

            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("provider");
        }

        [Fact]
        public void Constructor_ValidProvider_DoesNotThrow()
        {
            var provider = new Mock<IPersistenceProvider>();

            var act = () => new ChatMessageAttachmentRepository(provider.Object);

            act.Should().NotThrow();
        }

        [Fact]
        public async Task GetChatMessageAttachmentDtos_ConnectionFails_PropagatesExceptionAndPassesToken()
        {
            using var cts = new CancellationTokenSource();
            var provider = new Mock<IPersistenceProvider>(MockBehavior.Strict);
            provider
                .Setup(p => p.OpenConnectionAsync(cts.Token))
                .ThrowsAsync(new InvalidOperationException("connection failed"));

            var repository = new ChatMessageAttachmentRepository(provider.Object);

            var act = () => repository.GetChatMessageAttachmentDtos(Guid.NewGuid(), cts.Token);

            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("connection failed");
            provider.Verify(p => p.OpenConnectionAsync(cts.Token), Times.Once);
        }
    }
}
