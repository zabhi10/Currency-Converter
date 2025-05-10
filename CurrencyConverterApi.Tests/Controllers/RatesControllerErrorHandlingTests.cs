using CurrencyConverterApi.Controllers;
using CurrencyConverterApi.Models;
using CurrencyConverterApi.Services;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CurrencyConverterApi.Tests.Controllers
{
    public class RatesControllerErrorHandlingTests
    {
        private readonly Mock<ICurrencyService> _mockCurrencyService;
        private readonly RatesController _controller;
        private readonly Mock<ILogger<RatesController>> _mockLogger;
        private readonly Mock<IValidator<HistoricalRatesRequest>> _mockHistoricalRatesValidator;
        private readonly Mock<IValidator<LatestRatesRequest>> _mockLatestRatesValidator;
        private readonly Mock<IValidator<ConvertRequest>> _mockConvertValidator;

        public RatesControllerErrorHandlingTests()
        {
            _mockCurrencyService = new Mock<ICurrencyService>();
            _mockLogger = new Mock<ILogger<RatesController>>();
            _mockHistoricalRatesValidator = new Mock<IValidator<HistoricalRatesRequest>>();
            _mockLatestRatesValidator = new Mock<IValidator<LatestRatesRequest>>();
            _mockConvertValidator = new Mock<IValidator<ConvertRequest>>();

            _controller = new RatesController(
                _mockCurrencyService.Object,
                _mockLogger.Object,
                _mockHistoricalRatesValidator.Object,
                _mockLatestRatesValidator.Object,
                _mockConvertValidator.Object);

            // Assume validation passes for these error handling tests
            _mockLatestRatesValidator.Setup(v => v.ValidateAsync(It.IsAny<LatestRatesRequest>(), It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(new ValidationResult());
            _mockConvertValidator.Setup(v => v.ValidateAsync(It.IsAny<ConvertRequest>(), It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(new ValidationResult());
            _mockHistoricalRatesValidator.Setup(v => v.ValidateAsync(It.IsAny<HistoricalRatesRequest>(), It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(new ValidationResult());
        }

        [Fact]
        public async Task Convert_ServiceThrowsException_ReturnsInternalServerError()
        {
            // Arrange
            var request = new ConvertRequest { BaseCurrency = "USD", TargetCurrency = "EUR", Amount = 100m };
            _mockCurrencyService.Setup(s => s.ConvertAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<decimal>()))
                .ThrowsAsync(new Exception("Service unavailable"));

            // Act
            var result = await _controller.Convert(request);
            
            // Assert
            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status500InternalServerError, objectResult.StatusCode);
            Assert.IsType<ProblemDetails>(objectResult.Value);
        }

        [Fact]
        public async Task Latest_ServiceThrowsException_ReturnsInternalServerError()
        {
            // Arrange
            var request = new LatestRatesRequest { BaseCurrency = "USD" };
            _mockCurrencyService.Setup(s => s.GetLatestAsync(It.IsAny<string>()))
                .ThrowsAsync(new Exception("Service unavailable"));

            // Act
            var result = await _controller.Latest(request);

            // Assert
            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status500InternalServerError, objectResult.StatusCode);
            Assert.IsType<ProblemDetails>(objectResult.Value);
        }

        [Fact]
        public async Task History_ServiceThrowsException_ReturnsInternalServerError()
        {
            // Arrange
            var request = new HistoricalRatesRequest
            {
                BaseCurrency = "USD",
                StartDate = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                EndDate = new DateTime(2023, 1, 10, 0, 0, 0, DateTimeKind.Utc),
                Page = 1,
                PageSize = 10
            };
            _mockCurrencyService.Setup(s => s.GetHistoricalAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ThrowsAsync(new Exception("Service unavailable"));

            // Act
            var result = await _controller.History(request);

            // Assert
            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status500InternalServerError, objectResult.StatusCode);
            Assert.IsType<ProblemDetails>(objectResult.Value);
        }
    }
}
