using FluentValidation;
using FluentValidation.Results;

namespace HootOut.Contracts.ChatServers.Requests
{
    public sealed record CreateChatServerRequest
    {
        public string Name { get; init; } = string.Empty;

        public string Description { get; init; } = string.Empty;

        public ValidationResult Validate() => new CreateChatServerRequestValidator().Validate(this);
    }

    public class CreateChatServerRequestValidator : AbstractValidator<CreateChatServerRequest>
    {
        public CreateChatServerRequestValidator()
        {
            RuleFor(x => x.Name).NotEmpty();
        }
    }
}
