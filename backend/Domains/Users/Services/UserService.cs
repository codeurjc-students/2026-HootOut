using HootOut.Contracts.Common.Saver;
using HootOut.Contracts.Users.Dtos;
using HootOut.Contracts.Users.Dtos.Request;
using HootOut.Contracts.Users.Services;
using HootOut.Users.Entities;
using HootOut.Users.Mappers;
using HootOut.Users.Search;
using System.ComponentModel.DataAnnotations;

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

        public UserDto CreateUser(CreateUserRequest createUserRequest)
        {
            if (createUserRequest == null) throw new ArgumentNullException(nameof(createUserRequest));

            var newUser = Persistable.CreateNew<UserInfo>();
            newUser.Email = createUserRequest.Email;
            newUser.Username = createUserRequest.Username;
            newUser.Password = createUserRequest.Password;

            userSaver.Save(newUser);

            return newUser.ToDto();
        }

        public UserDto? GetUserDtoById(Guid id)
        {
            if (Guid.Empty == id)
            {
                throw new ValidationException("Invalid Request");
            }

            return userSearch.GetUserById(id)?.ToDto();
        }
    }
}
