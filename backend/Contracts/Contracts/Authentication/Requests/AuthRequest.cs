using FluentValidation;

namespace HootOut.Contracts.Authentication.Requests
{
    public class AuthRequest
    {
        public string? Email { get; set; }

        public string? Password { get; set; }

        public void Validate() => new AuthRequestValidation().ValidateAndThrow(this);
    }

    public class AuthRequestValidation : AbstractValidator<AuthRequest>
    {
        public AuthRequestValidation()
        {
            RuleFor(x => x.Email).NotEmpty().EmailAddress();
            RuleFor(x => x.Password).NotEmpty().MinimumLength(5);
        }
    }
}
