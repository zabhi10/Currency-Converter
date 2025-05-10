using CurrencyConverterApi.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Xunit;

namespace CurrencyConverterApi.Tests.Services
{
    public class ActivityTrackingTests
    {
        private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
        private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
        private readonly Mock<ILogger<FrankfurterCurrencyProvider>> _mockLogger;
        private readonly HttpClient _httpClient;
        private readonly FrankfurterCurrencyProvider _provider;
        
        public ActivityTrackingTests()
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
                    Content = new StringContent(JsonSerializer.Serialize(
                        new 
                        {
                            amount = 1,
                            @base = "USD",
                            date = "2023-05-08",
                            rates = new Dictionary<string, decimal>
                            {
                                { "EUR", 0.85m },
                                { "GBP", 0.75m }
                            }
                        }
                    ))
                });
        }
        
        [Fact]
        public async Task GetLatestAsync_CreatesActivity()
        {
            // Arrange
            Activity stoppedActivity = null;
            
            using var listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == "CurrencyConverterApi.FrankfurterProvider",
                Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllData,
                ActivityStarted = activity => {  },
                ActivityStopped = activity => 
                {
                    stoppedActivity = activity;
                }
            };
            
            ActivitySource.AddActivityListener(listener);
            
            // Act
            var baseCurrency = "USD";
            await _provider.GetLatestAsync(baseCurrency);
            
            // Assert
            Assert.NotNull(stoppedActivity);
            Assert.Equal("FrankfurterGetLatest", stoppedActivity.OperationName);
            Assert.Equal(baseCurrency, stoppedActivity.GetTagItem("currency.base")?.ToString());
        }
        
        [Fact]
        public async Task ConvertAsync_CreatesActivityWithTags()
        {
            // Arrange
            Activity stoppedActivity = null;
            
            using var listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == "CurrencyConverterApi.FrankfurterProvider",
                Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllData,
                ActivityStarted = activity => { },
                ActivityStopped = activity => 
                {
                    stoppedActivity = activity;
                }
            };
            
            ActivitySource.AddActivityListener(listener);
            
            // Act
            var baseCurrency = "USD";
            var targets = new[] { "EUR", "GBP" };
            var amount = 100m;
            await _provider.ConvertAsync(baseCurrency, targets, amount);
            
            // Assert
            Assert.NotNull(stoppedActivity);
            var capturedTags = new Dictionary<string, object>();
            if (stoppedActivity != null)
            {
                foreach (var tag in stoppedActivity.TagObjects)
                {
                    capturedTags[tag.Key] = tag.Value;
                }
            }

            Assert.Contains("currency.base", capturedTags.Keys);
            Assert.Contains("currency.targets", capturedTags.Keys);
            Assert.Contains("amount", capturedTags.Keys);
            Assert.Contains("http.url", capturedTags.Keys);
            Assert.Contains("http.method", capturedTags.Keys);
            
            Assert.Equal(baseCurrency, capturedTags["currency.base"]);
            Assert.Equal(string.Join(",", targets), capturedTags["currency.targets"]);
            Assert.Equal(amount, capturedTags["amount"]);
            Assert.Equal("GET", capturedTags["http.method"]);
        }
        
        [Fact]
        public async Task GetHistoricalAsync_ErrorSetsActivityStatus()
        {
            // Arrange
            ActivityStatusCode? capturedStatus = null;
            string capturedDescription = null;
            
            using var listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == "CurrencyConverterApi.FrankfurterProvider",
                Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllData,
                ActivityStopped = activity => 
                {
                    capturedStatus = activity.Status;
                    capturedDescription = activity.StatusDescription;
                }
            };
            
            ActivitySource.AddActivityListener(listener);
            
            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ThrowsAsync(new HttpRequestException("Test HTTP exception"));
            
            // Act & Assert
            var baseCurrency = "USD";
            var start = DateTime.Today.AddDays(-10);
            var end = DateTime.Today;
            
            await Assert.ThrowsAsync<HttpRequestException>(() => 
                _provider.GetHistoricalAsync(baseCurrency, start, end));
            
            Assert.Equal(ActivityStatusCode.Error, capturedStatus);
        }
    }
}
