using FluentAssertions;
using HootOut.Contracts.Common.Saver;
using HootOut.Contracts.Users.Dtos;
using HootOut.Contracts.Users.Dtos.Request;
using HootOut.Contracts.Users.Search;
using HootOut.Contracts.Users.Services;
using HootOut.Users.Entities;
using HootOut.Users.Mappers;
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

            m_UserSearch.Setup(x => x.GetUserDtos()).Returns(users.Select(x => x.ToDto()));
            m_UserSaver.Setup(x => x.Save(It.IsAny<UserInfo>())).Callback<UserInfo>(x => users.Add(x));
        }

        [Fact]
        public async Task GetUserDtos_Emtpy()
        {
            IUserService userService = new UserService(m_UserSaver.Object, m_UserSearch.Object);
            userService.GetUserDtos().Should().BeEmpty();
        }

        [Theory]
        [InlineData("email@test.com", "username1", "password12345")]
        public async Task GetUserDtos_One(string username, string email, string password)
        {
            IUserService userService = new UserService(m_UserSaver.Object, m_UserSearch.Object);

            userService.CreateUser(new CreateUserRequest
            {
                Username = username,
                Email = email,
                Password = password
            });

            IEnumerable<UserDto> users = userService.GetUserDtos();

            users.Should().HaveCount(1);

            UserDto user = users.Single();

            user.Uid.Should().NotBeEmpty();
            user.Username.Should().Be(username);
            user.Email.Should().Be(email);
            user.Password.Should().NotBe(password);

            m_UserSaver.Verify(x => x.Save(It.IsAny<UserInfo>()), Times.Once());
            m_UserSearch.Verify(x => x.GetUserDtos(), Times.Once());
        }

        [Fact]
        public async Task GetUserDtos_Many()
        {
            IUserService userService = new UserService(m_UserSaver.Object, m_UserSearch.Object);

            userService.CreateUser(new CreateUserRequest { Email = "1", Password = "1", Username = "1" });
            userService.CreateUser(new CreateUserRequest { Email = "2", Password = "2", Username = "2" });
            userService.CreateUser(new CreateUserRequest { Email = "3", Password = "3", Username = "3" });

            userService.GetUserDtos().Should().HaveCount(3);

            m_UserSaver.Verify(x => x.Save(It.IsAny<UserInfo>()), Times.Exactly(3));
            m_UserSearch.Verify(x => x.GetUserDtos(), Times.Once());
        }
    }
}
