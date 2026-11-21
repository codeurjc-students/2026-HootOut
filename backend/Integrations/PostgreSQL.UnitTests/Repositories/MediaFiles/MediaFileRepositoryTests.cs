using FluentAssertions;
using HootOut.CommonDomain.Persistence;
using HootOut.PostgreSQL.Repositories.MediaFiles;
using Moq;

namespace HootOut.PostgreSQL.UnitTests.Repositories.MediaFiles
{
    public class MediaFileRepositoryTests
    {
        [Fact]
        public void Constructor_NullProvider_ThrowsArgumentNullException()
        {
            var act = () => new MediaFileRepository(null!);

            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("provider");
        }

        [Fact]
        public void Constructor_ValidProvider_DoesNotThrow()
        {
            var provider = new Mock<IPersistenceProvider>();

            var act = () => new MediaFileRepository(provider.Object);

            act.Should().NotThrow();
        }

        [Fact]
        public async Task GetMediaFileByIdAsync_PassesCancellationTokenToProvider()
        {
            using var cts = new CancellationTokenSource();
            var provider = new Mock<IPersistenceProvider>(MockBehavior.Strict);
            provider
                .Setup(p => p.OpenConnectionAsync(cts.Token))
                .ThrowsAsync(new InvalidOperationException("connection failed"));

            var repository = new MediaFileRepository(provider.Object);

            var act = () => repository.GetMediaFileByIdAsync(Guid.NewGuid(), cts.Token);

            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("connection failed");
            provider.Verify(p => p.OpenConnectionAsync(cts.Token), Times.Once);
        }
    }
}
