using CurrencyConverterApi.Models;
using FluentValidation;
using System;

namespace CurrencyConverterApi.Validators
{
    public class HistoricalRatesRequestValidator : AbstractValidator<HistoricalRatesRequest>
    {
        public HistoricalRatesRequestValidator()
        {
            RuleFor(x => x.BaseCurrency)
                .NotEmpty().WithMessage("Base currency is required.")
                .Length(3).WithMessage("Base currency must be 3 characters long.")
                .Matches("^[a-zA-Z]+$").WithMessage("Base currency must contain only characters.");

            RuleFor(x => x.StartDate)
                .NotEmpty().WithMessage("Start date is required.")
                .LessThanOrEqualTo(x => x.EndDate).When(x => x.EndDate.HasValue).WithMessage("Start date must be before or same as end date.")
                .LessThanOrEqualTo(DateTime.UtcNow).WithMessage("Start date cannot be in the future.");

            RuleFor(x => x.EndDate)
                .NotEmpty().WithMessage("End date is required.")
                .GreaterThanOrEqualTo(x => x.StartDate).When(x => x.StartDate.HasValue).WithMessage("End date must be after or same as start date.")
                .LessThanOrEqualTo(DateTime.UtcNow).WithMessage("End date cannot be in the future.");

            RuleFor(x => x.Page)
                .GreaterThan(0).WithMessage("Page number must be greater than zero.");

            RuleFor(x => x.PageSize)
                .InclusiveBetween(1, 100).WithMessage("Page size must be between 1 and 100.");
        }
    }
}
