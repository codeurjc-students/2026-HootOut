using FluentAssertions;
using HootOut.CommonDomain.Persistence;
using HootOut.PostgreSQL.Repositories.Users;
using Moq;

namespace HootOut.PostgreSQL.UnitTests.Repositories.Users
{
    public class UserRepositoryTests
    {
        public static TheoryData<string> RepositoryMethods =>
        [
            nameof(UserRepository.GetAllUsersAsync),
            nameof(UserRepository.GetUserByIdAsync),
            nameof(UserRepository.GetUserByEmailAsync),
            nameof(UserRepository.GetUserByUsernameAsync)
        ];

        [Fact]
        public void Constructor_NullProvider_ThrowsArgumentNullException()
        {
            var act = () => new UserRepository(null!);

            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("provider");
        }

        [Fact]
        public void Constructor_ValidProvider_DoesNotThrow()
        {
            var provider = new Mock<IPersistenceProvider>();

            var act = () => new UserRepository(provider.Object);

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

            var repository = new UserRepository(provider.Object);

            var act = () => Invoke(repository, method, cts.Token);

            await act.Should().ThrowExactlyAsync<InvalidOperationException>()
                .WithMessage("connection failed");
            provider.Verify(p => p.OpenConnectionAsync(cts.Token), Times.Once);
        }

        private static Task Invoke(UserRepository repository, string method, CancellationToken ct) => method switch
        {
            nameof(UserRepository.GetAllUsersAsync) => repository.GetAllUsersAsync(ct),
            nameof(UserRepository.GetUserByIdAsync) => repository.GetUserByIdAsync(Guid.NewGuid(), ct),
            nameof(UserRepository.GetUserByEmailAsync) => repository.GetUserByEmailAsync("user@test.com", ct),
            nameof(UserRepository.GetUserByUsernameAsync) => repository.GetUserByUsernameAsync("username", ct),
            _ => throw new ArgumentOutOfRangeException(nameof(method), method, null)
        };
    }
}
