using FluentValidation;
using HootOut.Contracts.Common.Saver;
using HootOut.Contracts.Users.Dtos;
using HootOut.Contracts.Users.Dtos.Request;
using HootOut.Contracts.Users.Services;
using HootOut.Users.Entities;
using HootOut.Users.Mappers;
using HootOut.Users.Search;

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

        public async Task<UserDto> CreateUserAsync(CreateUserRequest createUserRequest, CancellationToken ct = default)
        {
            if (createUserRequest == null) throw new ArgumentNullException(nameof(createUserRequest));

            var newUser = Persistable.CreateNew<UserInfo>();
            newUser.Email = createUserRequest.Email;
            newUser.Username = createUserRequest.Username;
            newUser.Password = createUserRequest.Password;

            await userSaver.SaveAsync(newUser, ct);

            return newUser.ToDto();
        }

        public async Task<UserDto?> GetUserDtoByIdAsync(Guid id, CancellationToken ct = default)
        {
            if (Guid.Empty == id)
            {
                throw new ValidationException("Invalid Request");
            }

            return (await userSearch.GetUserByIdAsync(id, ct))?.ToDto();
        }
    }
}
