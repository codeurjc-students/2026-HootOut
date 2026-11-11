using FluentAssertions;
using HootOut.ChatServers.Entities;
using HootOut.CommonDomain.Persistence;
using HootOut.Contracts.Common;
using HootOut.Contracts.Common.Saver;
using HootOut.PostgreSQL.Repositories.ChatServers;
using Moq;

namespace HootOut.PostgreSQL.UnitTests.Repositories.ChatServers
{
    public class ServerInvitationRepositoryTests
    {
        public static TheoryData<string> RepositoryMethods =>
        [
            nameof(ServerInvitationRepository.GetServerInvitationDtosAsync),
            nameof(ServerInvitationRepository.GetUserServerInvitationDtosAsync),
            nameof(ServerInvitationRepository.GetServerInvitationByIdAsync),
            nameof(ServerInvitationRepository.AcceptServerInvitationAsync)
        ];

        [Fact]
        public void Constructor_NullProvider_ThrowsArgumentNullException()
        {
            var act = () => new ServerInvitationRepository(null!);

            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("provider");
        }

        [Fact]
        public void Constructor_ValidProvider_DoesNotThrow()
        {
            var provider = new Mock<IPersistenceProvider>();

            var act = () => new ServerInvitationRepository(provider.Object);

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

            var repository = new ServerInvitationRepository(provider.Object);

            var act = () => Invoke(repository, method, cts.Token);

            await act.Should().ThrowExactlyAsync<InvalidOperationException>()
                .WithMessage("connection failed");
            provider.Verify(p => p.OpenConnectionAsync(cts.Token), Times.Once);
        }

        private static Task Invoke(ServerInvitationRepository repository, string method, CancellationToken ct) => method switch
        {
            nameof(ServerInvitationRepository.GetServerInvitationDtosAsync) => repository.GetServerInvitationDtosAsync(Guid.NewGuid(), new PageRequest(), ct),
            nameof(ServerInvitationRepository.GetUserServerInvitationDtosAsync) => repository.GetUserServerInvitationDtosAsync(Guid.NewGuid(), new PageRequest(), ct),
            nameof(ServerInvitationRepository.GetServerInvitationByIdAsync) => repository.GetServerInvitationByIdAsync(Guid.NewGuid(), ct),
            nameof(ServerInvitationRepository.AcceptServerInvitationAsync) => repository.AcceptServerInvitationAsync(Persistable.CreateNew<ServerInvitation>(), ct),
            _ => throw new ArgumentOutOfRangeException(nameof(method), method, null)
        };
    }
}
