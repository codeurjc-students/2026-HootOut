using FluentValidation;
using HootOut.Contracts.ChatServers.Enums;

namespace HootOut.Contracts.ChatServers.Requests
{
    public sealed record UpdateServerInvitationRequest
    {
        public Guid Id { get; set; }
        public ServerInvitationStatus Status { get; set; }
        public void Validate() => new UpdateServerInvitationRequestValidator().ValidateAndThrow(this);
    }

    public class UpdateServerInvitationRequestValidator : AbstractValidator<UpdateServerInvitationRequest>
    {
        public UpdateServerInvitationRequestValidator()
        {
            RuleFor(x => x.Id).NotEmpty();
            RuleFor(x => x.Status).NotEmpty().IsInEnum();
        }
    }
}
