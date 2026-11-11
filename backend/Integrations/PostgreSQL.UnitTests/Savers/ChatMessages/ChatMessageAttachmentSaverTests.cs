using FluentAssertions;
using HootOut.ChatMessages.Entities;
using HootOut.CommonDomain.Persistence;
using HootOut.PostgreSQL.Savers.ChatMessages;
using Moq;

namespace HootOut.PostgreSQL.UnitTests.Savers.ChatMessages
{
    public class ChatMessageAttachmentSaverTests
    {
        [Fact]
        public void Constructor_NullProvider_ThrowsArgumentNullException()
        {
            var act = () => new ChatMessageAttachmentSaver(null!);

            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("provider");
        }

        [Fact]
        public void Constructor_ValidProvider_DoesNotThrow()
        {
            var provider = new Mock<IPersistenceProvider>();

            var act = () => new ChatMessageAttachmentSaver(provider.Object);

            act.Should().NotThrow();
        }

        [Fact]
        public async Task SaveManyAsync_NullItems_DoesNotOpenConnection()
        {
            // Strict mock without setups: any call to the provider would throw
            var provider = new Mock<IPersistenceProvider>(MockBehavior.Strict);
            var saver = new ChatMessageAttachmentSaver(provider.Object);

            var act = () => saver.SaveManyAsync(null!, TestContext.Current.CancellationToken);

            await act.Should().NotThrowAsync();
            provider.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task SaveManyAsync_EmptyItems_DoesNotOpenConnection()
        {
            var provider = new Mock<IPersistenceProvider>(MockBehavior.Strict);
            var saver = new ChatMessageAttachmentSaver(provider.Object);

            var act = () => saver.SaveManyAsync([], TestContext.Current.CancellationToken);

            await act.Should().NotThrowAsync();
            provider.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task SaveManyAsync_ConnectionFails_PropagatesProviderExceptionAndPassesToken()
        {
            using var cts = new CancellationTokenSource();
            var provider = new Mock<IPersistenceProvider>(MockBehavior.Strict);
            provider
                .Setup(p => p.OpenConnectionAsync(cts.Token))
                .ThrowsAsync(new InvalidOperationException("connection failed"));

            var saver = new ChatMessageAttachmentSaver(provider.Object);
            var attachment = new ChatMessageAttachment { Id = Guid.NewGuid(), MessageId = Guid.NewGuid(), MediaFileId = Guid.NewGuid() };

            var act = () => saver.SaveManyAsync([attachment], cts.Token);

            // Opening the connection happens outside the try/catch, so the exception is not wrapped
            await act.Should().ThrowExactlyAsync<InvalidOperationException>()
                .WithMessage("connection failed");
            provider.Verify(p => p.OpenConnectionAsync(cts.Token), Times.Once);
        }
    }
}
