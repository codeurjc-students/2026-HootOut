using FluentAssertions;
using HootOut.Contracts.Common.Saver;
using HootOut.Contracts.Users.Dtos;
using HootOut.Contracts.Users.Dtos.Request;
using HootOut.Contracts.Users.Services;
using HootOut.Users.Entities;
using HootOut.Users.Search;
using HootOut.Users.Services;
using Moq;

namespace HootOut.Users.UnitTests.Services
{
    public class UserServiceTests
    {
        Mock<ISaver<UserInfo>> m_UserSaver;
        Mock<IUserSearch> m_UserSearch;

        public UserServiceTests()
        {
            m_UserSaver = new Mock<ISaver<UserInfo>>();
            m_UserSearch = new Mock<IUserSearch>();

            List<UserInfo> users = new List<UserInfo>();

            m_UserSearch.Setup(
                x => x.GetUserByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .Returns((Guid id, CancellationToken _) => Task.FromResult(users.FirstOrDefault(x => x.Id == id)));

            m_UserSaver.Setup(x => x.SaveAsync(It.IsAny<UserInfo>(), It.IsAny<CancellationToken>())).Callback<UserInfo, CancellationToken>((x, _) => users.Add(x));
        }

        [Fact]
        public async Task GetUserById_Emtpy()
        {
            var ct = TestContext.Current.CancellationToken;
            IUserService userService = new UserService(m_UserSaver.Object, m_UserSearch.Object);
            (await userService.GetUserDtoByIdAsync(Guid.NewGuid(), ct)).Should().BeNull();
        }

        [Theory]
        [InlineData("email@test.com", "username1", "password12345")]
        public async Task GetUserDtoById_One(string username, string email, string password)
        {
            var ct = TestContext.Current.CancellationToken;
            IUserService userService = new UserService(m_UserSaver.Object, m_UserSearch.Object);

            var dto = await userService.CreateUserAsync(new CreateUserRequest
            {
                Username = username,
                Email = email,
                Password = password
            }, ct);

            UserDto? user = await userService.GetUserDtoByIdAsync(dto.Id, ct);

            user.Should().NotBeNull();

            user.Id.Should().NotBeEmpty();
            user.Username.Should().Be(username);
            user.Email.Should().Be(email);

            m_UserSaver.Verify(x => x.SaveAsync(It.IsAny<UserInfo>()), Times.Once());
            m_UserSearch.Verify(x => x.GetUserByIdAsync(dto.Id, ct), Times.Once());
        }

        [Fact]
        public async Task GetUserDtoById_Many()
        {
            var ct = TestContext.Current.CancellationToken;
            IUserService userService = new UserService(m_UserSaver.Object, m_UserSearch.Object);

            await userService.CreateUserAsync(new CreateUserRequest { Email = "1", Password = "1", Username = "1" }, ct);
            var dto = await userService.CreateUserAsync(new CreateUserRequest { Email = "2", Password = "2", Username = "2" }, ct);
            await userService.CreateUserAsync(new CreateUserRequest { Email = "3", Password = "3", Username = "3" }, ct);

            var user = await userService.GetUserDtoByIdAsync(dto.Id, ct);
            user.Should().NotBeNull();

            m_UserSaver.Verify(x => x.SaveAsync(It.IsAny<UserInfo>()), Times.Exactly(3));
            m_UserSearch.Verify(x => x.GetUserByIdAsync(dto.Id, ct), Times.Once());
        }
    }
}
