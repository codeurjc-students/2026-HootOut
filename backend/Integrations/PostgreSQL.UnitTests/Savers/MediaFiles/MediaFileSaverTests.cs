using FluentAssertions;
using HootOut.CommonDomain.Persistence;
using HootOut.MediaFiles.Entities;
using HootOut.PostgreSQL.Savers.MediaFiles;
using Moq;

namespace HootOut.PostgreSQL.UnitTests.Savers.MediaFiles
{
    public class MediaFileSaverTests
    {
        [Fact]
        public void Constructor_NullProvider_ThrowsArgumentNullException()
        {
            var act = () => new MediaFileSaver(null!);

            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("provider");
        }

        [Fact]
        public void Constructor_ValidProvider_DoesNotThrow()
        {
            var provider = new Mock<IPersistenceProvider>();

            var act = () => new MediaFileSaver(provider.Object);

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

            var saver = new MediaFileSaver(provider.Object);

            var act = () => saver.SaveAsync(new MediaFile { Id = Guid.NewGuid() }, cts.Token);

            // Opening the connection happens outside the try/catch, so the exception is not wrapped
            await act.Should().ThrowExactlyAsync<InvalidOperationException>()
                .WithMessage("connection failed");
            provider.Verify(p => p.OpenConnectionAsync(cts.Token), Times.Once);
        }
    }
}
