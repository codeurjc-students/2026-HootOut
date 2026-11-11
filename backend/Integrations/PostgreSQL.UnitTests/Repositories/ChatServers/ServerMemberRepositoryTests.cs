using FluentAssertions;
using HootOut.CommonDomain.Persistence;
using HootOut.Contracts.Common;
using HootOut.PostgreSQL.Repositories.ChatServers;
using Moq;

namespace HootOut.PostgreSQL.UnitTests.Repositories.ChatServers
{
    public class ServerMemberRepositoryTests
    {
        public static TheoryData<string> RepositoryMethods =>
        [
            nameof(ServerMemberRepository.GetServerMemberAsync),
            nameof(ServerMemberRepository.GetServerMemberDtosAsync)
        ];

        [Fact]
        public void Constructor_NullProvider_ThrowsArgumentNullException()
        {
            var act = () => new ServerMemberRepository(null!);

            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("provider");
        }

        [Fact]
        public void Constructor_ValidProvider_DoesNotThrow()
        {
            var provider = new Mock<IPersistenceProvider>();

            var act = () => new ServerMemberRepository(provider.Object);

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

            var repository = new ServerMemberRepository(provider.Object);

            var act = () => Invoke(repository, method, cts.Token);

            await act.Should().ThrowExactlyAsync<InvalidOperationException>()
                .WithMessage("connection failed");
            provider.Verify(p => p.OpenConnectionAsync(cts.Token), Times.Once);
        }

        private static Task Invoke(ServerMemberRepository repository, string method, CancellationToken ct) => method switch
        {
            nameof(ServerMemberRepository.GetServerMemberAsync) => repository.GetServerMemberAsync(Guid.NewGuid(), Guid.NewGuid(), ct),
            nameof(ServerMemberRepository.GetServerMemberDtosAsync) => repository.GetServerMemberDtosAsync(Guid.NewGuid(), new PageRequest(), ct),
            _ => throw new ArgumentOutOfRangeException(nameof(method), method, null)
        };
    }
}
