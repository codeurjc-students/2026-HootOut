using FluentValidation;
using FluentValidation.Results;
using HootOut.Contracts.Common.Saver;
using HootOut.Contracts.Users.Dtos;
using HootOut.Contracts.Users.Dtos.Request;
using HootOut.Contracts.Users.Services;
using HootOut.Users.Entities;
using HootOut.Users.Mappers;
using HootOut.Users.Repositories;

namespace HootOut.Users.Services
{
    public class UserService : IUserService
    {
        private ISaver<UserInfo> userSaver;
        private IUserRepository userRepository;

        public UserService(ISaver<UserInfo> userSaver, IUserRepository userRepository)
        {
            this.userSaver = userSaver ?? throw new ArgumentNullException(nameof(userSaver));
            this.userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        }

        public async Task<UserDto> CreateUserAsync(CreateUserRequest createUserRequest, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(createUserRequest);

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
                throw new ValidationException(new[] { new ValidationFailure("userId", "Invalid Request") }); 
            }

            return (await userRepository.GetUserByIdAsync(id, ct))?.ToDto();
        }

        public async Task<UserDto?> GetUserDtoByUsernameAsync(string username, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(username))
            {
                throw new ValidationException(new[] { new ValidationFailure("username", "username cannot be empty") });
            }

            return (await userRepository.GetUserByUsernameAsync(username, ct))?.ToDto();
        }
    }
}
