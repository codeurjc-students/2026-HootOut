using FluentValidation;

namespace HootOut.Contracts.Authentication.Requests
{
    public class AuthRegistrationRequest
    {
        public string? Username { get; set; }
        public string? Email { get; set; }
        public string? Password { get; set; }
        public void Validate() => new AuthRegistrationRequestValidator().ValidateAndThrow(this);
    }

    public class AuthRegistrationRequestValidator : AbstractValidator<AuthRegistrationRequest>
    {
        public AuthRegistrationRequestValidator()
        {
            RuleFor(x => x.Username).NotEmpty().MinimumLength(5).MaximumLength(50);
            RuleFor(x => x.Email).NotEmpty().EmailAddress().MinimumLength(5).MaximumLength(50);
            RuleFor(x => x.Password).NotEmpty().MinimumLength(5);
        }
    }
}
