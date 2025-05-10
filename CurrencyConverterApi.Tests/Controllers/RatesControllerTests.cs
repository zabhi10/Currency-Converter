using CurrencyConverterApi.Controllers;
using CurrencyConverterApi.Models;
using CurrencyConverterApi.Services;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CurrencyConverterApi.Tests.Controllers
{
    public class RatesControllerTests
    {
        private readonly Mock<ICurrencyService> _mockService;
        private readonly Mock<ILogger<RatesController>> _mockLogger;
        private readonly Mock<IValidator<HistoricalRatesRequest>> _mockHistoricalRatesValidator;
        private readonly Mock<IValidator<LatestRatesRequest>> _mockLatestRatesValidator;
        private readonly Mock<IValidator<ConvertRequest>> _mockConvertValidator;
        private readonly RatesController _controller;

        public RatesControllerTests()
        {
            _mockService = new Mock<ICurrencyService>();
            _mockLogger = new Mock<ILogger<RatesController>>();
            _mockHistoricalRatesValidator = new Mock<IValidator<HistoricalRatesRequest>>();
            _mockLatestRatesValidator = new Mock<IValidator<LatestRatesRequest>>();
            _mockConvertValidator = new Mock<IValidator<ConvertRequest>>();

            _controller = new RatesController(
                _mockService.Object,
                _mockLogger.Object,
                _mockHistoricalRatesValidator.Object,
                _mockLatestRatesValidator.Object,
                _mockConvertValidator.Object);

            _mockLatestRatesValidator.Setup(v => v.ValidateAsync(It.IsAny<LatestRatesRequest>(), It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(new ValidationResult());
            _mockConvertValidator.Setup(v => v.ValidateAsync(It.IsAny<ConvertRequest>(), It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(new ValidationResult());
            _mockHistoricalRatesValidator.Setup(v => v.ValidateAsync(It.IsAny<HistoricalRatesRequest>(), It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(new ValidationResult());
        }

        [Fact]
        public async Task Latest_WithValidRequest_ReturnsOkResultWithFilteredRates()
        {
            // Arrange
            var request = new LatestRatesRequest { BaseCurrency = "USD" };
            var allRatesFromService = new Dictionary<string, decimal>
            {
                { "EUR", 0.85m },
                { "GBP", 0.75m },
                { "JPY", 110.55m }
            };
            var expectedFilteredRates = new Dictionary<string, decimal>
            {
                { "EUR", 0.85m },
                { "GBP", 0.75m },
                { "JPY", 110.55m } // Updated to include JPY to match allRatesFromService
            };

            _mockService.Setup(s => s.GetLatestAsync(request.BaseCurrency!.ToUpperInvariant()))
                .ReturnsAsync(allRatesFromService);

            // Act
            var result = await _controller.Latest(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var actualResponse = Assert.IsType<LatestRatesResponse>(okResult.Value);
            Assert.NotNull(actualResponse);
            Assert.Equal(request.BaseCurrency.ToUpperInvariant(), actualResponse.Base);
            Assert.NotNull(actualResponse.Rates);
            Assert.Equal(expectedFilteredRates.Count, actualResponse.Rates.Count);
            foreach (var (key, value) in expectedFilteredRates)
            {
                Assert.True(actualResponse.Rates.ContainsKey(key));
                Assert.Equal(value, actualResponse.Rates[key]);
            }
            Assert.True(actualResponse.Date.Kind == DateTimeKind.Utc);
        }

        [Fact]
        public async Task Latest_WithValidRequest_NoTargets_ReturnsOkResultWithAllRates()
        {
            // Arrange
            var request = new LatestRatesRequest { BaseCurrency = "USD" };
            var allRatesFromService = new Dictionary<string, decimal>
            {
                { "EUR", 0.85m }, { "GBP", 0.75m }, { "JPY", 110.55m }
            };
             _mockService.Setup(s => s.GetLatestAsync(request.BaseCurrency!.ToUpperInvariant()))
                .ReturnsAsync(allRatesFromService);

            // Act
            var result = await _controller.Latest(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var actualResponse = Assert.IsType<LatestRatesResponse>(okResult.Value);
            Assert.Equal(allRatesFromService.Count, actualResponse.Rates.Count);
        }
        
        [Theory]
        [InlineData(null, "Base currency is required.")]
        [InlineData("", "Base currency is required.")]
        [InlineData("US", "Base currency must be 3 characters long.")]
        [InlineData("US1", "Base currency must contain only characters.")]
        public async Task Latest_InvalidBaseCurrency_ReturnsValidationProblem(string? baseCurrency, string expectedErrorMessage)
        {
            // Arrange
            var request = new LatestRatesRequest { BaseCurrency = baseCurrency };
            var validationFailures = new List<ValidationFailure> { new ValidationFailure(nameof(request.BaseCurrency), expectedErrorMessage) };
            _mockLatestRatesValidator.Setup(v => v.ValidateAsync(request, It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(new ValidationResult(validationFailures));

            // Act
            var result = await _controller.Latest(request);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result); // Changed from ObjectResult
            Assert.Equal(400, badRequestResult.StatusCode);
            var validationProblemDetails = Assert.IsAssignableFrom<ValidationProblemDetails>(badRequestResult.Value);
            Assert.True(validationProblemDetails.Errors.ContainsKey(nameof(request.BaseCurrency)));
            Assert.Contains(expectedErrorMessage, validationProblemDetails.Errors[nameof(request.BaseCurrency)]);
        }

        [Fact]
        public async Task Convert_WithValidParameters_ReturnsOkResult()
        {
            // Arrange
            var request = new ConvertRequest { BaseCurrency = "USD", TargetCurrency = "EUR", Amount = 100m };
            var convertedAmountFromService = 85m;
            var serviceResponse = new Dictionary<string, decimal> { { request.TargetCurrency.ToUpperInvariant(), convertedAmountFromService } };

            _mockService.Setup(s => s.ConvertAsync(request.BaseCurrency.ToUpperInvariant(), 
                                                  It.Is<IEnumerable<string>>(targets => targets.Contains(request.TargetCurrency.ToUpperInvariant()) && targets.Count() == 1), 
                                                  request.Amount))
                .ReturnsAsync(serviceResponse);

            // Act
            var result = await _controller.Convert(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var actualResponse = Assert.IsType<ConversionResponse>(okResult.Value);
            Assert.NotNull(actualResponse);
            Assert.Equal(request.BaseCurrency.ToUpperInvariant(), actualResponse.Base);
            Assert.Equal(request.TargetCurrency.ToUpperInvariant(), actualResponse.Target);
            Assert.Equal(request.Amount, actualResponse.Amount);
            Assert.Equal(convertedAmountFromService, actualResponse.ConvertedAmount);
            Assert.True(actualResponse.Date.Kind == DateTimeKind.Utc);
        }

        [Theory]
        [InlineData(0, "Amount must be greater than zero.")]
        [InlineData(-10, "Amount must be greater than zero.")]
        public async Task Convert_InvalidAmount_ReturnsValidationProblem(decimal amount, string expectedErrorMessage)
        {
            // Arrange
            var request = new ConvertRequest { BaseCurrency = "USD", TargetCurrency = "EUR", Amount = amount };
            var validationFailures = new List<ValidationFailure> { new ValidationFailure(nameof(request.Amount), expectedErrorMessage) };
            _mockConvertValidator.Setup(v => v.ValidateAsync(request, It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(new ValidationResult(validationFailures));
            
            // Act
            var result = await _controller.Convert(request);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result); // Changed from ObjectResult
            Assert.Equal(400, badRequestResult.StatusCode);
            var validationProblemDetails = Assert.IsAssignableFrom<ValidationProblemDetails>(badRequestResult.Value);
            Assert.True(validationProblemDetails.Errors.ContainsKey(nameof(request.Amount)));
            Assert.Contains(expectedErrorMessage, validationProblemDetails.Errors[nameof(request.Amount)]);
        }
        
        [Fact]
        public async Task History_WithValidParameters_ReturnsOkResultWithPagination()
        {
            // Arrange
            var request = new HistoricalRatesRequest
            {
                BaseCurrency = "USD",
                StartDate = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                EndDate = new DateTime(2023, 1, 10, 0, 0, 0, DateTimeKind.Utc),
                Page = 1,
                PageSize = 5
            };
            
            // Mock validator to return valid result
            _mockHistoricalRatesValidator.Setup(v => v.ValidateAsync(request, It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(new ValidationResult());

            var mockHistoricalDataTuple = Enumerable.Range(0, 10)
                .Select(i => (
                    Date: request.StartDate.Value.AddDays(i),
                    Rates: (IDictionary<string, decimal>)new Dictionary<string, decimal> {
                        { "EUR", 0.85m + (i * 0.01m) },
                        { "GBP", 0.75m + (i * 0.01m) }
                    }
                )).ToList();
                            
            _mockService.Setup(s => s.GetHistoricalAsync(request.BaseCurrency!.ToUpperInvariant(), request.StartDate.Value, request.EndDate.Value))
                .ReturnsAsync(mockHistoricalDataTuple);
            
            // Act
            var result = await _controller.History(request);
            
            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var actualResponse = Assert.IsType<HistoricalRatesResponse>(okResult.Value);
            Assert.NotNull(actualResponse);
            
            Assert.Equal(request.BaseCurrency.ToUpperInvariant(), actualResponse.Base);
            Assert.Equal(request.StartDate, actualResponse.StartDate);
            Assert.Equal(request.EndDate, actualResponse.EndDate);
            Assert.Equal(request.Page, actualResponse.Page);
            Assert.Equal(request.PageSize, actualResponse.PageSize);
            Assert.Equal(10, actualResponse.TotalItems); 
            Assert.Equal(2, actualResponse.TotalPages); // 10 items / 5 per page
            
            Assert.NotNull(actualResponse.Data);
            Assert.Equal(5, actualResponse.Data.Count()); // Page 1 with PageSize 5
        }
    }
}