using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CurrencyConverterApi.Models;
using CurrencyConverterApi.Tests.Integration.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using System.Text; 

namespace CurrencyConverterApi.Tests.Integration
{
    public class RatesIntegrationTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly HttpClient _client;
        private readonly CustomWebApplicationFactory _factory;

        public RatesIntegrationTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
        }

        private async Task<string?> GetValidTokenAsync(string clientId = "admin_client", string apiKey = "demo_api_key")
        {
            var loginRequest = new LoginRequest { ClientId = clientId, ApiKey = apiKey };
            var response = await _client.PostAsync("/api/v1/auth/token",
                new StringContent(JsonSerializer.Serialize(loginRequest), Encoding.UTF8, "application/json"));

            if (response.IsSuccessStatusCode)
            {
                var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponseModel>();
                return tokenResponse?.AccessToken;
            }
            return null;
        }

        private async Task<(HttpResponseMessage response, T? successContent, ProblemDetailsModel? problemDetails)> GetAndDeserializeAsync<T>(
            string url, string? token = null)
        {
            var requestMessage = new HttpRequestMessage(HttpMethod.Get, url);
            if (!string.IsNullOrWhiteSpace(token))
            {
                requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            var response = await _client.SendAsync(requestMessage);

            if (response.IsSuccessStatusCode)
            {
                T? successModel = default;
                try
                {
                    successModel = await response.Content.ReadFromJsonAsync<T>();
                }
                catch (JsonException ex) { 
                    System.Diagnostics.Debug.WriteLine($"Success GET deserialization error for type {typeof(T).Name} from URL {url}: {ex.Message}. Content: {await response.Content.ReadAsStringAsync()}"); 
                }
                return (response, successModel, null);
            }
            else
            {
                ProblemDetailsModel? problem = null;
                try
                {
                    problem = await response.Content.ReadFromJsonAsync<ProblemDetailsModel>();
                }
                catch (JsonException ex) { 
                    System.Diagnostics.Debug.WriteLine($"Error GET deserialization error for ProblemDetailsModel from URL {url}: {ex.Message}. Content: {await response.Content.ReadAsStringAsync()}");
                }
                return (response, default(T), problem);
            }
        }


        [Fact]
        public async Task GetLatest_ValidRequest_WithToken_ReturnsOk()
        {
            // Arrange
            var token = await GetValidTokenAsync();
            Assert.NotNull(token);
            var url = "/api/v1/Rates/latest?baseCurrency=USD";

            // Act
            var (response, rateResponse, problemDetails) = await GetAndDeserializeAsync<RateResponseModel>(url, token);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.NotNull(rateResponse);
            Assert.Equal("USD", rateResponse.Base);
            Assert.NotNull(rateResponse.Rates);
            Assert.True(rateResponse.Rates.ContainsKey("EUR"));
            Assert.True(rateResponse.Rates.ContainsKey("GBP"));
            Assert.Null(problemDetails);
        }

        [Fact]
        public async Task GetLatest_NoToken_ReturnsUnauthorized()
        {
            // Arrange
            var url = "/api/v1/Rates/latest?base=USD";

            // Act
            var (response, _, problemDetails) = await GetAndDeserializeAsync<RateResponseModel>(url);

            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.NotNull(problemDetails);
        }
        
        [Fact]
        public async Task GetLatest_InvalidToken_ReturnsUnauthorized()
        {
            // Arrange
            var url = "/api/v1/Rates/latest?base=USD";

            // Act
            var (response, _, problemDetails) = await GetAndDeserializeAsync<RateResponseModel>(url, "invalid.token.value");

            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.NotNull(problemDetails);
        }


        [Theory]
        [InlineData("baseCurrency=USD", HttpStatusCode.OK, null, null)] 
        [InlineData("", HttpStatusCode.BadRequest, "BaseCurrency", "Base currency is required.")] 
        public async Task GetLatest_ParameterValidation_ReturnsExpectedStatus(string query, HttpStatusCode expectedStatus, string? expectedErrorProperty, string? expectedErrorMessage)
        {
            // Arrange
            var token = await GetValidTokenAsync();
            Assert.NotNull(token);
            var url = $"/api/v1/Rates/latest?{query}";

            // Act
            var (response, _, problemDetails) = await GetAndDeserializeAsync<RateResponseModel>(url, token);

            // Assert
            Assert.Equal(expectedStatus, response.StatusCode);
            if (expectedStatus != HttpStatusCode.OK)
            {
                Assert.NotNull(problemDetails);
                Assert.NotNull(problemDetails.Errors);
                Assert.True(problemDetails.Errors.ContainsKey(expectedErrorProperty!));
                Assert.Contains(expectedErrorMessage, problemDetails.Errors[expectedErrorProperty!]);
            }
        }


        [Fact]
        public async Task Convert_ValidRequest_WithToken_ReturnsOk()
        {
            // Arrange
            var token = await GetValidTokenAsync();
            Assert.NotNull(token);
            var url = "/api/v1/Rates/convert?baseCurrency=USD&targetCurrency=EUR&amount=100"; 

            // Act
            var (response, conversionResponse, problemDetails) = await GetAndDeserializeAsync<ConversionResponseModel>(url, token);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.NotNull(conversionResponse);
            Assert.Equal("USD", conversionResponse.Base);
            Assert.Equal("EUR", conversionResponse.Target); 
            Assert.True(conversionResponse.ConvertedAmount > 0); 
            Assert.Null(problemDetails);
        }
        
        [Fact]
        public async Task Convert_NoToken_ReturnsUnauthorized()
        {
            // Arrange
            var url = "/api/v1/Rates/convert?baseCurrency=USD&targetCurrency=EUR&amount=10";

            // Act
            var (response, _, problemDetails) = await GetAndDeserializeAsync<ConversionResponseModel>(url);

            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.NotNull(problemDetails);
        }

        [Theory]
        [InlineData("baseCurrency=USD&targetCurrency=EUR&amount=0", HttpStatusCode.BadRequest, "Amount", "Amount must be greater than zero.")]
        [InlineData("baseCurrency=USD&targetCurrency=EUR&amount=-10", HttpStatusCode.BadRequest, "Amount", "Amount must be greater than zero.")]
        [InlineData("baseCurrency=USD&targetCurrency=EUR&amount=abc", HttpStatusCode.BadRequest, "Amount", "The value 'abc' is not valid for Amount.")] // Actual error for type conversion
        [InlineData("baseCurrency=USD&targetCurrency=EUR", HttpStatusCode.BadRequest, "Amount", "Amount must be greater than zero.")] // Updated expected message
        [InlineData("baseCurrency=USD&amount=100", HttpStatusCode.BadRequest, "TargetCurrency", "Target currency is required.")]
        [InlineData("targetCurrency=EUR&amount=100", HttpStatusCode.BadRequest, "BaseCurrency", "Base currency is required.")]
        public async Task Convert_ParameterValidation_ReturnsBadRequest(string query, HttpStatusCode expectedStatus, string expectedErrorProperty, string expectedErrorMessage)
        {
            // Arrange
            var token = await GetValidTokenAsync();
            Assert.NotNull(token);
            var url = $"/api/v1/Rates/convert?{query}";

            // Act
            var (response, _, problemDetails) = await GetAndDeserializeAsync<ConversionResponseModel>(url, token);

            // Assert
            Assert.Equal(expectedStatus, response.StatusCode);
            Assert.NotNull(problemDetails);
            Assert.NotNull(problemDetails.Errors);
            Assert.True(problemDetails.Errors.ContainsKey(expectedErrorProperty));
            Assert.Contains(expectedErrorMessage, problemDetails.Errors[expectedErrorProperty]);
        }

        [Fact]
        public async Task GetHistorical_ValidRequest_WithToken_ReturnsOk()
        {
            // Arrange
            var token = await GetValidTokenAsync();
            Assert.NotNull(token);
            var startDate = DateTime.Now.AddDays(-7).ToString("yyyy-MM-dd");
            var endDate = DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd");
            var url = $"/api/v1/Rates/history?baseCurrency=USD&start={startDate}&end={endDate}";
            // Act
            var (response, historicalResponse, problemDetails) = await GetAndDeserializeAsync<HistoricalRatesResponse>(url, token);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.NotNull(historicalResponse);
            Assert.NotNull(historicalResponse.Data);
            Assert.True(historicalResponse.Data.Any());
            Assert.All(historicalResponse.Data, item => Assert.NotNull(item.Rates));
            Assert.Null(problemDetails);
        }
        
        [Fact]
        public async Task GetHistorical_NoToken_ReturnsUnauthorized()
        {
            // Arrange
             var startDate = DateTime.Now.AddDays(-7).ToString("yyyy-MM-dd");
            var endDate = DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd");
            var url = $"/api/v1/Rates/history?baseCurrency=USD&start={startDate}&end={endDate}"; 

            // Act
            var (response, _, problemDetails) = await GetAndDeserializeAsync<HistoricalRatesResponse>(url);

            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.NotNull(problemDetails);
        }

        [Theory]
        [InlineData("baseCurrency=USD&start=2023-01-01&end=2023-01-05", HttpStatusCode.OK, null, null)]
        [InlineData("baseCurrency=USD&start=2023-01-05&end=2023-01-01", HttpStatusCode.BadRequest, "StartDate", "Start date must be before or same as end date.")] 
        [InlineData("baseCurrency=USD&start=2023-01-01", HttpStatusCode.BadRequest, "EndDate", "End date is required.")] 
        [InlineData("baseCurrency=USD&end=2023-01-05", HttpStatusCode.BadRequest, "StartDate", "Start date is required.")]
        [InlineData("baseCurrency=USD&start=2023-06-01&end=2023-06-05", HttpStatusCode.OK, null, null)]
        public async Task GetHistorical_ParameterValidation_ReturnsExpectedStatus(string query, HttpStatusCode expectedStatus, string? expectedErrorProperty, string? expectedErrorMessage)
        {
            // Arrange
            var token = await GetValidTokenAsync();
            Assert.NotNull(token);
            var url = $"/api/v1/Rates/history?{query}";

            // Act
            var (response, _, problemDetails) = await GetAndDeserializeAsync<HistoricalRatesResponse>(url, token);

            // Assert
            Assert.Equal(expectedStatus, response.StatusCode);
            if (expectedStatus != HttpStatusCode.OK)
            {
                Assert.NotNull(problemDetails);
                Assert.NotNull(problemDetails.Errors);
                Assert.True(problemDetails.Errors.ContainsKey(expectedErrorProperty!));
                Assert.Contains(expectedErrorMessage, problemDetails.Errors[expectedErrorProperty!]);
            }
        }
    }
}
