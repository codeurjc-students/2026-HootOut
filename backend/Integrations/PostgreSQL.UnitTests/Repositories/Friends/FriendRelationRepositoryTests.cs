using FluentAssertions;
using HootOut.CommonDomain.Persistence;
using HootOut.Contracts.Common;
using HootOut.PostgreSQL.Repositories.Friends;
using Moq;

namespace HootOut.PostgreSQL.UnitTests.Repositories.Friends
{
    public class FriendRelationRepositoryTests
    {
        public static TheoryData<string> RepositoryMethods =>
        [
            nameof(FriendRelationRepository.GetUserFriendsAsync),
            nameof(FriendRelationRepository.GetDirectConverationByConversationAndUserId)
        ];

        [Fact]
        public void Constructor_NullProvider_ThrowsArgumentNullException()
        {
            var act = () => new FriendRelationRepository(null!);

            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("provider");
        }

        [Fact]
        public void Constructor_ValidProvider_DoesNotThrow()
        {
            var provider = new Mock<IPersistenceProvider>();

            var act = () => new FriendRelationRepository(provider.Object);

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

            var repository = new FriendRelationRepository(provider.Object);

            var act = () => Invoke(repository, method, cts.Token);

            await act.Should().ThrowExactlyAsync<InvalidOperationException>()
                .WithMessage("connection failed");
            provider.Verify(p => p.OpenConnectionAsync(cts.Token), Times.Once);
        }

        private static Task Invoke(FriendRelationRepository repository, string method, CancellationToken ct) => method switch
        {
            nameof(FriendRelationRepository.GetUserFriendsAsync) => repository.GetUserFriendsAsync(new PageRequest(), Guid.NewGuid(), ct),
            nameof(FriendRelationRepository.GetDirectConverationByConversationAndUserId) => repository.GetDirectConverationByConversationAndUserId(Guid.NewGuid(), Guid.NewGuid(), ct),
            _ => throw new ArgumentOutOfRangeException(nameof(method), method, null)
        };
    }
}
