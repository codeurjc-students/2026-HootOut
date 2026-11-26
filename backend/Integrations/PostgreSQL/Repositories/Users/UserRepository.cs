using Dapper;
using HootOut.CommonDomain.Persistence;
using HootOut.Contracts.Common.Dto;
using HootOut.Contracts.Users.Dtos;
using HootOut.Users.Entities;
using HootOut.Users.Repositories;

namespace HootOut.PostgreSQL.Repositories.Users
{
    public class UserRepository : IUserRepository
    {
        private IPersistenceProvider provider;

        public UserRepository(IPersistenceProvider provider)
        {
            this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        private const string UserColumns =
        """ m."Id", m."Username", m."PasswordHash" as Password, m."Email", m."ProfilePictureId", m."CreatedAt" """;
        public async Task<IEnumerable<UserInfo>> GetAllUsersAsync(CancellationToken ct = default)
        {
            await using var conn = await provider.OpenConnectionAsync(ct);

            return await conn.QueryAsync<UserInfo>($"""SELECT {UserColumns} FROM "HootOut"."UserInfo" m """, ct);
        }

        public async Task<UserInfo?> GetUserByEmailAsync(string email, CancellationToken ct = default)
        {
            await using var conn = await provider.OpenConnectionAsync(ct);

            return await conn.QuerySingleOrDefaultAsync<UserInfo>(new CommandDefinition(
                $"""SELECT {UserColumns} FROM "HootOut"."UserInfo" m where lower("Email") = lower(@Email)""",
                 new { Email = email }, cancellationToken: ct));
        }

        public async Task<UserInfo?> GetUserByIdAsync(Guid id, CancellationToken ct = default)
        {
            await using var conn = await provider.OpenConnectionAsync(ct);

            return await conn.QuerySingleOrDefaultAsync<UserInfo>(new CommandDefinition(
                $"""SELECT {UserColumns} FROM "HootOut"."UserInfo" m where "Id" = @Id""",
                    new { Id = id }, cancellationToken: ct));
        }
        public async Task<UserDto?> GetUserDtoByIdAsync(Guid id, CancellationToken ct = default)
        {
            await using var conn = await provider.OpenConnectionAsync(ct);

            return (await conn.QueryAsync<UserDto?, PictureDto?, UserDto?>(new CommandDefinition(
                $"""
                SELECT {UserColumns},
                mf."Id" as MediaFileId,
                mf."FileName",
                mf."ContentType"
                FROM "HootOut"."UserInfo" m
                LEFT JOIN "HootOut"."MediaFile" mf
                    ON m."ProfilePictureId" = mf."Id"
                WHERE m."Id" = @Id
                """,
                new { Id = id }, cancellationToken: ct), (user, profilePicture) =>
                {
                    if (user != null && profilePicture != null)
                    {
                        user.ProfilePicture = profilePicture;
                    }
                    return user;
                }, splitOn: "Id, MediaFileId"))?.FirstOrDefault();
        }

        public async Task<UserInfo?> GetUserByUsernameAsync(string username, CancellationToken ct = default)
        {
            await using var conn = await provider.OpenConnectionAsync(ct);

            return await conn.QuerySingleOrDefaultAsync<UserInfo>(new CommandDefinition(
                $"""SELECT {UserColumns} FROM "HootOut"."UserInfo" m where lower("Username") = lower(@Username)""",
                    new { Username = username }, cancellationToken: ct));
        }

        public async Task UpdateProfilePictureAsync(Guid mediaFileId, Guid userId, CancellationToken ct)
        {
            await using var conn = await provider.OpenConnectionAsync(ct);

            await conn.ExecuteAsync(
                new CommandDefinition("""
                UPDATE "HootOut"."UserInfo"
                    SET "ProfilePictureId" = @MediaFileId
                WHERE "Id" = @UserId;
                """, new { UserId = userId, MediaFileId = mediaFileId },
                cancellationToken: ct));
        }
    }
}