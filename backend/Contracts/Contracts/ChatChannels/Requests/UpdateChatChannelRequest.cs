using FluentValidation;

namespace HootOut.Contracts.ChatChannels.Requests
{
    public sealed class UpdateChatChannelRequest
    {
        public Guid Id { get; set; }
        public string Name { get; init; } = string.Empty;
        public void Validate() => new UpdateChatChannelRequestValidator().ValidateAndThrow(this);
    }

    public class UpdateChatChannelRequestValidator : AbstractValidator<UpdateChatChannelRequest>
    {
        public UpdateChatChannelRequestValidator()
        {
            RuleFor(x => x.Id).NotEmpty();
            RuleFor(x => x.Name).NotEmpty().MinimumLength(3);
        }
    }
}
