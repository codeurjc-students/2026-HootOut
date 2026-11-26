namespace HootOut.Contracts.Users.Requests
{
    public sealed record UpdateProfilePictureRequest
    {
        public Guid MediaFileId { get; set; }
    }
}
