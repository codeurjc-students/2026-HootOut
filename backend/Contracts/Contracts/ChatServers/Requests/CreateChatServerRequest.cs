using FluentValidation;

namespace HootOut.Contracts.ChatServers.Requests
{
    public sealed record CreateChatServerRequest
    {
        public string Name { get; init; } = string.Empty;

        public string Description { get; init; } = string.Empty;

        public void Validate() => new CreateChatServerRequestValidator().ValidateAndThrow(this);
    }

    public class CreateChatServerRequestValidator : AbstractValidator<CreateChatServerRequest>
    {
        public CreateChatServerRequestValidator()
        {
            RuleFor(x => x.Name).NotEmpty().MinimumLength(5).MaximumLength(50);
            RuleFor(x => x.Description).MaximumLength(200);
        }
    }
}
