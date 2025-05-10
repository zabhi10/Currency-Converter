using FluentValidation;
using CurrencyConverterApi.Models;

namespace CurrencyConverterApi.Validators
{
    public class LoginRequestValidator : AbstractValidator<LoginRequest>
    {
        public LoginRequestValidator()
        {
            RuleFor(x => x.ClientId)
                .NotEmpty().WithMessage("Client ID is required.")
                .MaximumLength(50).WithMessage("Client ID cannot be longer than 50 characters.");

            RuleFor(x => x.ApiKey)
                .NotEmpty().WithMessage("API key is required.")
                .MinimumLength(10).WithMessage("API key must be at least 10 characters long.")
                .MaximumLength(100).WithMessage("API key cannot be longer than 100 characters.");
        }
    }
}
