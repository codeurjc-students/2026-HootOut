using FluentAssertions;
using HootOut.CommonDomain.Persistence;
using HootOut.Contracts.Common.Saver;
using HootOut.Friends.Entities;
using HootOut.PostgreSQL.Savers.Friends;
using Moq;

namespace HootOut.PostgreSQL.UnitTests.Savers.Friends
{
    public class FriendRequestSaverTests
    {
        [Fact]
        public void Constructor_NullProvider_ThrowsArgumentNullException()
        {
            var act = () => new FriendRequestSaver(null!);

            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("provider");
        }

        [Fact]
        public void Constructor_ValidProvider_DoesNotThrow()
        {
            var provider = new Mock<IPersistenceProvider>();

            var act = () => new FriendRequestSaver(provider.Object);

            act.Should().NotThrow();
        }

        [Fact]
        public async Task SaveAsync_ConnectionFails_PropagatesProviderExceptionAndPassesToken()
        {
            using var cts = new CancellationTokenSource();
            var provider = new Mock<IPersistenceProvider>(MockBehavior.Strict);
            provider
                .Setup(p => p.OpenConnectionAsync(cts.Token))
                .ThrowsAsync(new InvalidOperationException("connection failed"));

            var saver = new FriendRequestSaver(provider.Object);

            var act = () => saver.SaveAsync(Persistable.CreateNew<FriendRequest>(), cts.Token);

            // Opening the connection happens outside the try/catch, so the exception is not wrapped
            await act.Should().ThrowExactlyAsync<InvalidOperationException>()
                .WithMessage("connection failed");
            provider.Verify(p => p.OpenConnectionAsync(cts.Token), Times.Once);
        }
    }
}
