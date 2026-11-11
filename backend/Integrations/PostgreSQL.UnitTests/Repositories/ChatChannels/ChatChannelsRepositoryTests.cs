using FluentAssertions;
using HootOut.CommonDomain.Persistence;
using HootOut.PostgreSQL.Repositories.ChatChannels;
using Moq;

namespace HootOut.PostgreSQL.UnitTests.Repositories.ChatChannels
{
    public class ChatChannelRepositoryTests
    {
        public static TheoryData<string> RepositoryMethods =>
        [
            nameof(ChatChannelRepository.GetChannelsByServerId),
            nameof(ChatChannelRepository.GetChannelByIdAsync)
        ];

        [Fact]
        public void Constructor_NullProvider_ThrowsArgumentNullException()
        {
            var act = () => new ChatChannelRepository(null!);

            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("persistenceProvider");
        }

        [Fact]
        public void Constructor_ValidProvider_DoesNotThrow()
        {
            var provider = new Mock<IPersistenceProvider>();

            var act = () => new ChatChannelRepository(provider.Object);

            act.Should().NotThrow();
        }

        [Theory]
        [MemberData(nameof(RepositoryMethods))]
        public async Task AnyMethod_ConnectionFails_PropagatesProviderExceptionAndPassesToken(string method)
        {
            using var cts = new CancellationTokenSource();
            var provider = new Mock<IPersistenceProvider>(MockBehavior.Strict);
            provider
                .Setup(p => p.OpenConnectionAsync(cts.Token))
                .ThrowsAsync(new InvalidOperationException("connection failed"));

            var repository = new ChatChannelRepository(provider.Object);

            var act = () => Invoke(repository, method, cts.Token);

            await act.Should().ThrowExactlyAsync<InvalidOperationException>()
                .WithMessage("connection failed");
            provider.Verify(p => p.OpenConnectionAsync(cts.Token), Times.Once);
        }

        private static Task Invoke(ChatChannelRepository repository, string method, CancellationToken ct) => method switch
        {
            nameof(ChatChannelRepository.GetChannelsByServerId) => repository.GetChannelsByServerId(Guid.NewGuid(), ct),
            nameof(ChatChannelRepository.GetChannelByIdAsync) => repository.GetChannelByIdAsync(Guid.NewGuid(), ct),
            _ => throw new ArgumentOutOfRangeException(nameof(method), method, null)
        };
    }
}
