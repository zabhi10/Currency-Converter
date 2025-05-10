using CurrencyConverterApi.Models;
using FluentValidation;

namespace CurrencyConverterApi.Validators
{
    public class LatestRatesRequestValidator : AbstractValidator<LatestRatesRequest>
    {
        public LatestRatesRequestValidator()
        {
            RuleFor(x => x.BaseCurrency)
                .NotEmpty().WithMessage("Base currency is required.")
                .Length(3).WithMessage("Base currency must be 3 characters long.")
                .Matches("^[a-zA-Z]+$").WithMessage("Base currency must contain only characters.");
        }
    }
}
