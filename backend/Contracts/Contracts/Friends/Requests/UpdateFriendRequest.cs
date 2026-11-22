using FluentValidation;
using HootOut.Contracts.Common.Enums;

namespace HootOut.Contracts.Friends.Requests
{
    public sealed record UpdateFriendRequest
    {
        public Guid Id { get; set; }
        public InvitationStatus Status { get; set; }
        public void Validate() => new UpdateFriendInvitationRequestValidator().ValidateAndThrow(this);
    }

    public class UpdateFriendInvitationRequestValidator : AbstractValidator<UpdateFriendRequest>
    {
        public UpdateFriendInvitationRequestValidator()
        {
            RuleFor(x => x.Id).NotEmpty();
            RuleFor(x => x.Status).NotEmpty().IsInEnum();
        }
    }
}
