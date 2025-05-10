using CurrencyConverterApi.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System.Net;
using System.Text.Json;
using Xunit;

namespace CurrencyConverterApi.Tests.Services
{
    public class FrankfurterCurrencyProviderTests
    {
        private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
        private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
        private readonly Mock<ILogger<FrankfurterCurrencyProvider>> _mockLogger;
        private readonly HttpClient _httpClient;
        private FrankfurterCurrencyProvider _provider;

        public FrankfurterCurrencyProviderTests()
        {
            _mockHttpClientFactory = new Mock<IHttpClientFactory>();
            _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            _mockLogger = new Mock<ILogger<FrankfurterCurrencyProvider>>();

            _httpClient = new HttpClient(_mockHttpMessageHandler.Object)
            {
                BaseAddress = new Uri("https://api.frankfurter.app")
            };

            _mockHttpClientFactory
                .Setup(f => f.CreateClient("Frankfurter"))
                .Returns(_httpClient);

            _provider = new FrankfurterCurrencyProvider(_mockHttpClientFactory.Object, _mockLogger.Object);
        }

        private void SetupHttpResponse(HttpStatusCode statusCode, string content)
        {
            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = statusCode,
                    Content = new StringContent(content)
                });
            _provider = new FrankfurterCurrencyProvider(_mockHttpClientFactory.Object, _mockLogger.Object); // Recreate to use updated handler
        }

        [Fact]
        public async Task GetLatestAsync_InvalidBaseCurrency_ThrowsException()
        {
            // Arrange
            SetupHttpResponse(HttpStatusCode.NotFound, "{\"message\":\"Not found\"}");

            // Act & Assert
            await Assert.ThrowsAsync<HttpRequestException>(() => _provider.GetLatestAsync("INVALID"));
        }

        [Fact]
        public async Task ConvertAsync_EmptyTargets_ReturnsEmptyRates()
        {
            // Arrange
            SetupHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(new { amount = 1, @base = "USD", date = "2023-01-01", rates = new Dictionary<string, decimal>() }));

            // Act
            var result = await _provider.ConvertAsync("USD", new List<string>(), 100m);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task ConvertAsync_NullTargets_ThrowsArgumentNullException() // Assuming targets should not be null
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => _provider.ConvertAsync("USD", null, 100m));
        }

        [Fact]
        public async Task GetHistoricalAsync_ApiReturnsError_ThrowsException()
        {
            // Arrange
            SetupHttpResponse(HttpStatusCode.InternalServerError, "Internal Server Error");

            // Act & Assert
            await Assert.ThrowsAsync<HttpRequestException>(() => _provider.GetHistoricalAsync("USD", DateTime.Today.AddDays(-1), DateTime.Today));
        }

        [Fact]
        public async Task GetLatestAsync_ApiReturnsUnexpectedContent_ThrowsJsonException()
        {
            // Arrange
            SetupHttpResponse(HttpStatusCode.OK, "This is not valid JSON");

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => _provider.GetLatestAsync("USD")); // Changed to InvalidOperationException
        }

        [Fact]
        public async Task ConvertAsync_ApiReturnsUnexpectedContent_ThrowsJsonException()
        {
            // Arrange
            SetupHttpResponse(HttpStatusCode.OK, "This is not valid JSON");

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => _provider.ConvertAsync("USD", new[] { "EUR" }, 100m)); // Changed to InvalidOperationException
        }

        [Fact]
        public async Task GetHistoricalAsync_ApiReturnsUnexpectedContent_ThrowsJsonException()
        {
            // Arrange
            SetupHttpResponse(HttpStatusCode.OK, "This is not valid JSON");

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => _provider.GetHistoricalAsync("USD", DateTime.Today.AddDays(-1), DateTime.Today)); // Changed to InvalidOperationException
        }

        [Fact]
        public async Task GetLatestAsync_NullResponse_ThrowsInvalidOperationException()
        {
            // Arrange
            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = null // Null content
                });
            _provider = new FrankfurterCurrencyProvider(_mockHttpClientFactory.Object, _mockLogger.Object);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => _provider.GetLatestAsync("USD"));
        }

         [Fact]
        public async Task ConvertAsync_NullResponse_ThrowsInvalidOperationException()
        {
            // Arrange
            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = null // Null content
                });
            _provider = new FrankfurterCurrencyProvider(_mockHttpClientFactory.Object, _mockLogger.Object);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => _provider.ConvertAsync("USD", new [] {"EUR"}, 100m));
        }

        [Fact]
        public async Task GetHistoricalAsync_NullResponse_ThrowsInvalidOperationException()
        {
             // Arrange
            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = null // Null content
                });
            _provider = new FrankfurterCurrencyProvider(_mockHttpClientFactory.Object, _mockLogger.Object);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => _provider.GetHistoricalAsync("USD", DateTime.Today.AddDays(-1), DateTime.Today));
        }
    }
}
