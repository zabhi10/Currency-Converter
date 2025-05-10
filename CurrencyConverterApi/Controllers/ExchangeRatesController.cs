using CurrencyConverterApi.Models;
using CurrencyConverterApi.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace CurrencyConverterApi.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]")]
    public class RatesController : ControllerBase
    {
        private readonly ICurrencyService _service;
        private readonly ILogger<RatesController> _logger;
        private readonly IValidator<HistoricalRatesRequest> _historicalRatesValidator;
        private readonly IValidator<LatestRatesRequest> _latestRatesValidator;
        private readonly IValidator<ConvertRequest> _convertValidator;
        private static readonly ActivitySource _activitySource = new ActivitySource("CurrencyConverterApi");

        public RatesController(ICurrencyService service, 
                             ILogger<RatesController> logger, 
                             IValidator<HistoricalRatesRequest> historicalRatesValidator,
                             IValidator<LatestRatesRequest> latestRatesValidator,
                             IValidator<ConvertRequest> convertValidator)
        {
            _service = service;
            _logger = logger;
            _historicalRatesValidator = historicalRatesValidator;
            _latestRatesValidator = latestRatesValidator;
            _convertValidator = convertValidator;
        }

        /// <summary>
        /// Gets the latest exchange rates for a specific base currency
        /// </summary>
        /// <param name="request">The request containing base currency and optional targets.</param>
        /// <returns>A dictionary of the latest exchange rates</returns>
        [HttpGet("latest")]
        [Authorize(Policy = "User")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(LatestRatesResponse))]
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ValidationProblemDetails))]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(ProblemDetails))]
        public async Task<IActionResult> Latest([FromQuery] LatestRatesRequest request)
        {
            using var activity = _activitySource.StartActivity("GetLatestRates");

            var validationResult = await _latestRatesValidator.ValidateAsync(request);
            if (!validationResult.IsValid)
            {
                foreach (var error in validationResult.Errors)
                {
                    ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
                }
                return new BadRequestObjectResult(new ValidationProblemDetails(ModelState));
            }
            
            var upperBaseCurrency = request.BaseCurrency!.ToUpperInvariant(); // Validator ensures not null
            activity?.SetTag("currency.base", upperBaseCurrency);

            _logger.LogInformation("Getting latest exchange rates for base currency: {BaseCurrency}", upperBaseCurrency);

            try
            {
                var allRates = await _service.GetLatestAsync(upperBaseCurrency);

                if (allRates == null)
                {
                    ModelState.AddModelError(nameof(request.BaseCurrency), $"Could not retrieve rates for base currency '{upperBaseCurrency}'.");
                    return new BadRequestObjectResult(new ValidationProblemDetails(ModelState));
                }
                return Ok(new LatestRatesResponse
                {
                    Base = upperBaseCurrency,
                    Date = DateTime.UtcNow, 
                    Rates = allRates
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while getting latest rates for {BaseCurrency}", upperBaseCurrency);
                return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails 
                { 
                    Title = "An unexpected error occurred.", 
                    Status = StatusCodes.Status500InternalServerError 
                });
            }
        }

        /// <summary>
        /// Converts an amount from one currency to another
        /// </summary>
        /// <param name="request">The request containing base currency, target currency, and amount.</param>
        /// <returns>The converted amount</returns>
        [HttpGet("convert")]
        [Authorize(Policy = "User")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ConversionResponse))]
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ValidationProblemDetails))]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(ProblemDetails))]
        public async Task<IActionResult> Convert([FromQuery] ConvertRequest request)
        {
            using var activity = _activitySource.StartActivity("ConvertCurrency");

            var validationResult = await _convertValidator.ValidateAsync(request);
            if (!validationResult.IsValid)
            {
                foreach (var error in validationResult.Errors)
                {
                    ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
                }
                return new BadRequestObjectResult(new ValidationProblemDetails(ModelState));
            }

            activity?.SetTag("currency.base", request.BaseCurrency);
            activity?.SetTag("currency.target", request.TargetCurrency);
            activity?.SetTag("amount", request.Amount);

            _logger.LogInformation("Converting {Amount} {BaseCurrency} to {TargetCurrency}", request.Amount, request.BaseCurrency, request.TargetCurrency);
            
            var upperBaseCurrency = request.BaseCurrency!.ToUpperInvariant(); // Validator ensures not null
            var upperTargetCurrency = request.TargetCurrency!.ToUpperInvariant(); // Validator ensures not null

            try
            {
                var convertedAmountsDict = await _service.ConvertAsync(upperBaseCurrency, new List<string> { upperTargetCurrency }, request.Amount);

                if (convertedAmountsDict != null && convertedAmountsDict.TryGetValue(upperTargetCurrency, out var convertedValue))
                {
                    return Ok(new ConversionResponse
                    {
                        Base = upperBaseCurrency,
                        Target = upperTargetCurrency,
                        Amount = request.Amount,
                        ConvertedAmount = convertedValue,
                        Date = DateTime.UtcNow
                    });
                }
                else
                {
                    _logger.LogWarning("Could not convert {Amount} from {From} to {To}. Service returned no value or error.", request.Amount, upperBaseCurrency, upperTargetCurrency);
                    ModelState.AddModelError(string.Empty, $"Could not convert from {upperBaseCurrency} to {upperTargetCurrency}. Check currency codes or ensure rates are available.");
                    return new BadRequestObjectResult(new ValidationProblemDetails(ModelState));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while converting {Amount} {BaseCurrency} to {TargetCurrency}", request.Amount, upperBaseCurrency, upperTargetCurrency);
                return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails 
                { 
                    Title = "An unexpected error occurred.", 
                    Detail = ex.Message, 
                    Status = StatusCodes.Status500InternalServerError 
                });
            }
        }

        /// <summary>
        /// Retrieves historical exchange rates for a given period with pagination
        /// </summary>
        /// <param name="request">The request containing base currency, date range, and pagination parameters.</param>
        /// <returns>Historical exchange rates for the given period</returns>
        [HttpGet("history")]
        [Authorize(Policy = "Admin")] // Add Authorize attribute
        [ProducesResponseType(typeof(HistoricalRatesResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)] // Add 401 response type
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> History([FromQuery] HistoricalRatesRequest request)
        {
            var validationResult = await _historicalRatesValidator.ValidateAsync(request);
            if (!validationResult.IsValid)
            {
                foreach (var error in validationResult.Errors)
                {
                    ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
                }
                return new BadRequestObjectResult(new ValidationProblemDetails(ModelState));
            }

            var upperBaseCurrency = request.BaseCurrency!.ToUpperInvariant(); // Validator ensures not null

            try
            {
                var historicalData = await _service.GetHistoricalAsync(upperBaseCurrency, request.StartDate!.Value, request.EndDate!.Value);

                if (historicalData == null || !historicalData.Any())
                {
                    _logger.LogInformation("No historical data found for {BaseCurrency} between {StartDate} and {EndDate}", upperBaseCurrency, request.StartDate.Value, request.EndDate.Value);
                    return Ok(new HistoricalRatesResponse
                    {
                        Base = upperBaseCurrency,
                        StartDate = request.StartDate.Value,
                        EndDate = request.EndDate.Value,
                        Page = request.Page,
                        PageSize = request.PageSize,
                        TotalItems = 0,
                        TotalPages = 0,
                        Data = Enumerable.Empty<HistoricalRatesResponse.DailyRates>()
                    });
                }
                
                var totalItems = historicalData.Count();
                var totalPages = (int)Math.Ceiling(totalItems / (double)request.PageSize);

                var paginatedData = historicalData
                    .Skip((request.Page - 1) * request.PageSize)
                    .Take(request.PageSize)
                    .Select(d => new HistoricalRatesResponse.DailyRates { Date = d.Date, Rates = d.Rates })
                    .ToList();

                return Ok(new HistoricalRatesResponse
                {
                    Base = upperBaseCurrency,
                    StartDate = request.StartDate.Value,
                    EndDate = request.EndDate.Value,
                    Page = request.Page,
                    PageSize = request.PageSize,
                    TotalItems = totalItems,
                    TotalPages = totalPages,
                    Data = paginatedData
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while getting historical rates for {BaseCurrency} from {StartDate} to {EndDate}", upperBaseCurrency, request.StartDate, request.EndDate);
                return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails 
                { 
                    Title = "An unexpected error occurred.", 
                    Status = StatusCodes.Status500InternalServerError 
                });
            }
        }
    }
}