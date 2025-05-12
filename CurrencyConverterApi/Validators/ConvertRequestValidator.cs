using CurrencyConverterApi.Models;
using FluentValidation;

namespace CurrencyConverterApi.Validators
{
    public class ConvertRequestValidator : AbstractValidator<ConvertRequest>
    {
        private readonly List<string> _excludedCurrencies = new List<string> { "TRY", "PLN", "THB", "MXN" };
        public ConvertRequestValidator()
        {
            RuleFor(x => x.BaseCurrency)
            .NotEmpty().WithMessage("Base currency is required.")
            .Length(3).WithMessage("Base currency must be 3 characters long.")
            .Matches("^[a-zA-Z]+$").WithMessage("Base currency must contain only characters.")
            .Must(currency => currency != null && !_excludedCurrencies.Contains(currency.ToUpper()))
            .WithMessage(x => $"Base currency '{x.BaseCurrency}' is not supported.");

            RuleFor(x => x.TargetCurrency)
            .NotEmpty().WithMessage("Target currency is required.")
            .Length(3).WithMessage("Target currency must be 3 characters long.")
            .Matches("^[a-zA-Z]+$").WithMessage("Target currency must contain only characters.")
            .Must(currency => currency != null && !_excludedCurrencies.Contains(currency.ToUpper()))
            .WithMessage(x => $"Target currency '{x.TargetCurrency}' is not supported.");

            RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Amount must be greater than zero.");

            RuleFor(x => x)
            .Must(x => x.BaseCurrency != x.TargetCurrency)
            .WithMessage("Base currency and target currency cannot be the same.")
            .When(x => !string.IsNullOrEmpty(x.BaseCurrency) && !string.IsNullOrEmpty(x.TargetCurrency) && x.BaseCurrency.Length == 3 && x.TargetCurrency.Length == 3);
        }
    }
}
