using HootOut.Contracts.Common.Saver;
using HootOut.Contracts.Users.Dtos;
using HootOut.Contracts.Users.Dtos.Request;
using HootOut.Contracts.Users.Search;
using HootOut.Contracts.Users.Services;
using HootOut.Users.Entities;
using System.Security.Cryptography;

namespace HootOut.Users.Services
{
    public class UserService : IUserService
    {
        private ISaver<UserInfo> userSaver;
        private IUserSearch userSearch;

        public UserService(ISaver<UserInfo> userSaver, IUserSearch userSearch)
        {
            this.userSaver = userSaver ?? throw new ArgumentNullException(nameof(userSaver));
            this.userSearch = userSearch ?? throw new ArgumentNullException(nameof(userSearch));
        }

        public Guid CreateUser(CreateUserRequest createUserRequest)
        {
            if (createUserRequest == null) throw new ArgumentNullException(nameof(createUserRequest));

            var newUser = new UserInfo
            {
                Uid = Guid.CreateVersion7(),
                Email = createUserRequest.Email,
                Username = createUserRequest.Username,
                Password = "*********" // TO-DO proper security
            };

            userSaver.Save(newUser);

            return newUser.Uid;
        }

        public IEnumerable<UserDto> GetUserDtos()
        {
            return userSearch.GetUserDtos();
        }
    }
}
