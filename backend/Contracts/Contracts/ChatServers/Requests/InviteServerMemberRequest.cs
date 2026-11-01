using FluentValidation;

namespace HootOut.Contracts.ChatServers.Requests
{
    public sealed record InviteServerMemberRequest
    {
        public Guid ServerId { get; set; } = Guid.Empty;
        public string Username { get; set; } = string.Empty;
        public void Validate() => new InviteServerMemberRequestValidator().ValidateAndThrow(this);
    }

    public class InviteServerMemberRequestValidator : AbstractValidator<InviteServerMemberRequest>
    {
        public InviteServerMemberRequestValidator()
        {
            RuleFor(x => x.ServerId).NotEmpty();
            RuleFor(x => x.Username).NotEmpty().MinimumLength(5).MaximumLength(100);
        }
    }
}
