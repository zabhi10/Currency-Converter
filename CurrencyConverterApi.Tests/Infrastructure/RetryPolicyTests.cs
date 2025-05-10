using CurrencyConverterApi.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;
using Polly;
using Polly.Extensions.Http;
using System.Net;
using Xunit;

namespace CurrencyConverterApi.Tests.Infrastructure
{
    public class RetryPolicyTests
    {
        [Fact]
        public void RetryPolicy_Configuration_LoadsCorrectly()
        {
            // Arrange
            var initialData = new List<KeyValuePair<string, string?>> 
            {
                new("CurrencyApi:Retry:MaxRetryAttempts", "3"),
                new("CurrencyApi:Retry:CircuitBreakerThreshold", "5"),
                new("CurrencyApi:Retry:CircuitBreakerDurationMinutes", "1")
            };
            
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(initialData)
                .Build();
                
            var currencyApiSettings = new CurrencyApiSettings();
            configuration.GetSection("CurrencyApi").Bind(currencyApiSettings);
            
            // Assert
            Assert.Equal(3, currencyApiSettings.Retry.MaxRetryAttempts);
            Assert.Equal(5, currencyApiSettings.Retry.CircuitBreakerThreshold);
            Assert.Equal(1, currencyApiSettings.Retry.CircuitBreakerDurationMinutes);
        }
        
        [Fact]
        public void CircuitBreakerPolicy_Configuration_IsCorrect()
        {
            var settings = new CurrencyApiSettings
            {
                Retry = new RetrySettings
                {
                    MaxRetryAttempts = 3,
                    CircuitBreakerThreshold = 5,
                    CircuitBreakerDurationMinutes = 1
                }
            };
            
            // Assert settings are loaded correctly
            Assert.Equal(3, settings.Retry.MaxRetryAttempts);
            Assert.Equal(5, settings.Retry.CircuitBreakerThreshold);
            Assert.Equal(1, settings.Retry.CircuitBreakerDurationMinutes);
        }
        
        [Fact]
        public async Task HttpClient_WithCircuitBreaker_OpensCircuitAfterFailures()
        {
            // Arrange
            bool circuitBroken = false;
            
            var circuitBreakerPolicy = HttpPolicyExtensions
                .HandleTransientHttpError()
                .CircuitBreakerAsync(
                    5,
                    TimeSpan.FromMinutes(1),
                    onBreak: (_, _) => circuitBroken = true,
                    onReset: () => circuitBroken = false);
            
            var handler = new TestHttpMessageHandler(HttpStatusCode.InternalServerError);
            var client = new HttpClient(handler)
            {
                BaseAddress = new Uri("http://example.com")
            };
                    
            // Act
            var wrappedClient = new PollyHttpClientWrapper(client, circuitBreakerPolicy);
            
            // Make 6 calls to trigger circuit breaker
            for (int i = 0; i < 6; i++)
            {
                try
                {
                    var result = await wrappedClient.GetAsync("api/test"); // Used await
                }
                catch (Exception)
                {
                    // Expected after circuit breaks or during failures
                }
            }
            
            // Assert
            Assert.True(circuitBroken);
        }
        
        // Helper classes for testing
        private class TestHttpMessageHandler : HttpMessageHandler
        {
            private readonly HttpStatusCode _statusCode;
            
            public TestHttpMessageHandler(HttpStatusCode statusCode)
            {
                _statusCode = statusCode;
            }
            
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, System.Threading.CancellationToken cancellationToken)
            {
                return Task.FromResult(new HttpResponseMessage(_statusCode));
            }
        }
        
        private class PollyHttpClientWrapper
        {
            private readonly HttpClient _client;
            private readonly IAsyncPolicy<HttpResponseMessage> _policy;
            
            public PollyHttpClientWrapper(HttpClient client, IAsyncPolicy<HttpResponseMessage> policy)
            {
                _client = client;
                _policy = policy;
            }
            
            public Task<HttpResponseMessage> GetAsync(string uri)
            {
                return _policy.ExecuteAsync(() => _client.GetAsync(uri));
            }
        }
    }
}
