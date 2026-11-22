using FluentValidation;

namespace HootOut.Contracts.Friends.Requests
{
    public sealed class SendFriendRequest
    {
        public string Username { get; set; } = string.Empty;
        public void Validate() => new FriendRequestValidator().ValidateAndThrow(this);
    }

    public class FriendRequestValidator : AbstractValidator<SendFriendRequest>
    {
        public FriendRequestValidator()
        {
            RuleFor(x => x.Username).NotEmpty().MinimumLength(5).MaximumLength(100);
        }
    }
}
