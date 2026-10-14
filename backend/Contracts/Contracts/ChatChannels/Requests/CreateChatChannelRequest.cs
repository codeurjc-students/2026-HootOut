using FluentValidation;

namespace HootOut.Contracts.ChatChannels.Requests
{
    public sealed record CreateChatChannelRequest
    {
        public string Name { get; init; } = string.Empty;

        public Guid ChatServerId { get; init; }

        public void Validate() => new CreateChatChannelRequestValidator().ValidateAndThrow(this);
    }

    public class CreateChatChannelRequestValidator : AbstractValidator<CreateChatChannelRequest>
    {
        public CreateChatChannelRequestValidator()
        {
            RuleFor(x => x.ChatServerId).NotEmpty();
            RuleFor(x => x.Name).NotEmpty();
        }
    }
}
