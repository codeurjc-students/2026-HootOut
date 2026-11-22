using FluentValidation;

namespace HootOut.Contracts.ChatMessage.Requests
{
    public class CreateChatMessageRequest
    {
        public Guid ChatConversationId { get; set; }
        public Guid AuthorId { get; set; }
        public string Content { get; set; } = string.Empty;
        public IEnumerable<CreateChatAttachmentRequest> Attachments { get; set; } = Enumerable.Empty<CreateChatAttachmentRequest>();

        public void Validate() => new CreateChatMessageRequestValidator().ValidateAndThrow(this);
    }

    public class CreateChatMessageRequestValidator : AbstractValidator<CreateChatMessageRequest>
    {
        public const string EmptyMessage = "The message must have content or attachments";

        public CreateChatMessageRequestValidator()
        {
            RuleFor(x => x.Content).MaximumLength(6000);
            RuleFor(x => x.ChatConversationId).NotEmpty();
            RuleFor(x => x.AuthorId).NotEmpty();
            RuleFor(x => x.Attachments).NotNull();
            RuleForEach(x => x.Attachments).ChildRules(attachment =>
            {
                attachment.RuleFor(x => x.MediaFileId).NotEmpty();
            });

            // A message needs text, attachments or both
            RuleFor(x => x)
                .Must(x => !string.IsNullOrWhiteSpace(x.Content) || (x.Attachments != null && x.Attachments.Any()))
                .WithName("Content")
                .WithMessage(EmptyMessage);
        }
    }
}
