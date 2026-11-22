using FluentAssertions;
using HootOut.CommonDomain.Persistence;
using HootOut.Contracts.Common;
using HootOut.Contracts.Common.Saver;
using HootOut.Friends.Entities;
using HootOut.PostgreSQL.Repositories.Friends;
using Moq;

namespace HootOut.PostgreSQL.UnitTests.Repositories.Friends
{
    public class FriendRequestRepositoryTests
    {
        public static TheoryData<string> RepositoryMethods =>
        [
            nameof(FriendRequestRepository.GetFriendRequestByIdAsync),
            nameof(FriendRequestRepository.GetReceivedFriendRequestsAsync),
            nameof(FriendRequestRepository.GetSentFriendRequestsAsync),
            nameof(FriendRequestRepository.AcceptFriendRequestAsync)
        ];

        [Fact]
        public void Constructor_NullProvider_ThrowsArgumentNullException()
        {
            var act = () => new FriendRequestRepository(null!);

            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("provider");
        }

        [Fact]
        public void Constructor_ValidProvider_DoesNotThrow()
        {
            var provider = new Mock<IPersistenceProvider>();

            var act = () => new FriendRequestRepository(provider.Object);

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

            var repository = new FriendRequestRepository(provider.Object);

            var act = () => Invoke(repository, method, cts.Token);

            // In AcceptFriendRequestAsync the connection is also opened outside the try/catch, so nothing is wrapped
            await act.Should().ThrowExactlyAsync<InvalidOperationException>()
                .WithMessage("connection failed");
            provider.Verify(p => p.OpenConnectionAsync(cts.Token), Times.Once);
        }

        private static Task Invoke(FriendRequestRepository repository, string method, CancellationToken ct) => method switch
        {
            nameof(FriendRequestRepository.GetFriendRequestByIdAsync) => repository.GetFriendRequestByIdAsync(Guid.NewGuid(), ct),
            nameof(FriendRequestRepository.GetReceivedFriendRequestsAsync) => repository.GetReceivedFriendRequestsAsync(new PageRequest(), Guid.NewGuid(), ct),
            nameof(FriendRequestRepository.GetSentFriendRequestsAsync) => repository.GetSentFriendRequestsAsync(new PageRequest(), Guid.NewGuid(), ct),
            nameof(FriendRequestRepository.AcceptFriendRequestAsync) => repository.AcceptFriendRequestAsync(Persistable.CreateNew<FriendRequest>(), ct),
            _ => throw new ArgumentOutOfRangeException(nameof(method), method, null)
        };
    }
}
