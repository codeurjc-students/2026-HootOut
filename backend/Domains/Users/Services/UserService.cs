using FluentValidation;
using FluentValidation.Results;
using HootOut.CommonDomain.FileStorage;
using HootOut.Contracts.Common.Dto;
using HootOut.Contracts.Common.Saver;
using HootOut.Contracts.MediaFile.Services;
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
        private IMediaFileService mediaFileService;
        private IMediaUrlService mediaUrlService;

        public UserService(
            ISaver<UserInfo> userSaver,
            IUserRepository userRepository,
            IMediaFileService mediaFileService,
            IMediaUrlService mediaUrlService)
        {
            this.userSaver = userSaver ?? throw new ArgumentNullException(nameof(userSaver));
            this.userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
            this.mediaFileService = mediaFileService ?? throw new ArgumentNullException(nameof(mediaFileService));
            this.mediaUrlService = mediaUrlService ?? throw new ArgumentNullException(nameof(mediaUrlService));
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
                throw new ValidationException(new[] { new ValidationFailure("UserId", "Invalid Request") });
            }

            var userDto = await userRepository.GetUserDtoByIdAsync(id, ct);

            if (userDto?.ProfilePicture != null)
            {
                userDto.ProfilePicture.Url = await mediaUrlService.GetViewUrlAsync(userDto.ProfilePicture.MediaFileId, userDto.ProfilePicture.ContentType, userDto.ProfilePicture.FileName) ?? "";
            }

            return userDto;
        }

        public async Task<UserDto?> GetUserDtoByUsernameAsync(string username, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(username))
            {
                throw new ValidationException(new[] { new ValidationFailure("Username", "Username cannot be empty") });
            }

            return (await userRepository.GetUserByUsernameAsync(username, ct))?.ToDto();
        }

        public async Task<PictureDto> UpdateProfilePictureAsync(Guid mediaFileId, Guid userId, CancellationToken ct)
        {
            if (Guid.Empty == mediaFileId)
            {
                throw new ValidationException(new[] { new ValidationFailure("MediaFileId", "Profile picture cannot be empty") });
            }

            var mediaFile = await mediaFileService.GetMediaFileByIdAsync(mediaFileId, ct);
            if (mediaFile == null || mediaFile.OwnerId != userId || !AllowedProfilePictureTypes.Contains(mediaFile.ContentType))
            {
                throw new ValidationException(new[] { new ValidationFailure("MediaFileId", "Invalid profile picture file") });
            }

            await userRepository.UpdateProfilePictureAsync(mediaFileId, userId, ct);

            var profilePicture = new PictureDto
            {
                MediaFileId = mediaFile.Id,
                ContentType = mediaFile.ContentType,
                FileName = mediaFile.FileName
            };

            profilePicture.Url = await mediaUrlService.GetViewUrlAsync(mediaFile.Id, mediaFile.ContentType, mediaFile.FileName) ?? ""; 

            return profilePicture;
        }

        private static readonly HashSet<string> AllowedProfilePictureTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            // Images (no SVG: it can carry scripts)
            "image/jpeg",
            "image/png",
            "image/webp",
            "image/tiff",
            "image/bmp"
        };
    }
}
