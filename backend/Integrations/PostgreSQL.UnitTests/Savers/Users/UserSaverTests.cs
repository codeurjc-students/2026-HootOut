using FluentAssertions;
using HootOut.CommonDomain.Persistence;
using HootOut.Contracts.Common.Saver;
using HootOut.PostgreSQL.Savers.Users;
using HootOut.Users.Entities;
using Moq;

namespace HootOut.PostgreSQL.UnitTests.Savers.Users
{
    public class UserSaverTests
    {
        [Fact]
        public void Constructor_NullProvider_ThrowsArgumentNullException()
        {
            var act = () => new UserSaver(null!);

            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("provider");
        }

        [Fact]
        public void Constructor_ValidProvider_DoesNotThrow()
        {
            var provider = new Mock<IPersistenceProvider>();

            var act = () => new UserSaver(provider.Object);

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

            var saver = new UserSaver(provider.Object);

            var act = () => saver.SaveAsync(Persistable.CreateNew<UserInfo>(), cts.Token);

            // Opening the connection happens outside the try/catch, so the exception is not wrapped
            await act.Should().ThrowExactlyAsync<InvalidOperationException>()
                .WithMessage("connection failed");
            provider.Verify(p => p.OpenConnectionAsync(cts.Token), Times.Once);
        }
    }
}
