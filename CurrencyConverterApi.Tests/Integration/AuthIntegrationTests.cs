using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CurrencyConverterApi.Models;
using CurrencyConverterApi.Tests.Integration.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Mvc; 
using Xunit;
using System.Net.Http.Headers;

namespace CurrencyConverterApi.Tests.Integration;

public class AuthIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public AuthIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    private async Task<(HttpResponseMessage response, T? successContent, ProblemDetails? problemDetails)> PostAndDeserializeAsync<T>(
        string url, object requestBody)
    {
        var httpRequest = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
        var response = await _client.PostAsync(url, httpRequest);

        if (response.IsSuccessStatusCode)
        {
            T? successModel = default;
            try
            {
                successModel = await response.Content.ReadFromJsonAsync<T>();
            }
            catch (JsonException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Success response deserialization error: {ex.Message}");
            }
            return (response, successModel, null);
        }
        else
        {
            ProblemDetails? problem = null;
            try
            {
                problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
            }
            catch (JsonException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error response deserialization error: {ex.Message}");
            }
            return (response, default(T), problem);
        }
    }

    private async Task<(HttpResponseMessage response, T? successContent, ProblemDetails? problemDetails)> GetAndDeserializeAsync<T>(
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
            catch (JsonException ex) 
            {
                System.Diagnostics.Debug.WriteLine($"Success GET deserialization error for type {typeof(T).Name} from URL {url}: {ex.Message}. Content: {await response.Content.ReadAsStringAsync()}"); 
            }
            return (response, successModel, null);
        }
        else
        {
            ProblemDetails? problem = null;
            try 
            { 
                problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(); 
            }
            catch (JsonException ex) 
            {
                System.Diagnostics.Debug.WriteLine($"Error GET deserialization error for ProblemDetails from URL {url}: {ex.Message}. Content: {await response.Content.ReadAsStringAsync()}"); 
            }
            return (response, default(T), problem);
        }
    }

    [Fact]
    public async Task GetToken_WithValidApiKey_ReturnsOkAndToken()
    {
        // Arrange
        var request = new LoginRequest { ClientId = "admin_client", ApiKey = "demo_api_key" }; 

        // Act
        var (response, tokenResponse, _) = await PostAndDeserializeAsync<TokenResponseModel>("/api/v1/auth/token", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(tokenResponse);
        Assert.False(string.IsNullOrWhiteSpace(tokenResponse.AccessToken));
        Assert.True(tokenResponse.ExpiresIn > 0);
        Assert.True(tokenResponse.ExpiresAt > tokenResponse.IssuedAt);
    }

    [Fact]
    public async Task GetToken_WithInvalidApiKey_ReturnsUnauthorized()
    {
        // Arrange
        var request = new LoginRequest { ClientId = "test_client_invalid", ApiKey = "invalid_api_key_12345" };

        // Act
        var (response, _, problemDetails) = await PostAndDeserializeAsync<TokenResponseModel>("/api/v1/auth/token", request);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotNull(problemDetails);
        Assert.Equal("Authentication Failed", problemDetails.Title);
        Assert.Equal("Invalid API key", problemDetails.Detail);
        Assert.Equal((int)HttpStatusCode.Unauthorized, problemDetails.Status);
    }

    [Fact]
    public async Task GetToken_WithMissingClientIdInRequest_ReturnsBadRequest()
    {
        // Arrange
        var client = _factory.CreateClient();
        var requestBody = new { ApiKey = "demo_api_key" };
        var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync("/api/v1/auth/token", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var responseString = await response.Content.ReadAsStringAsync();
        var problemDetails = JsonSerializer.Deserialize<ValidationProblemDetails>(responseString, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(problemDetails);
        Assert.Equal("One or more validation errors occurred.", problemDetails.Title);
        Assert.NotNull(problemDetails.Errors);
        Assert.True(problemDetails.Errors.ContainsKey("ClientId"));
        Assert.Contains("Client ID is required.", problemDetails.Errors["ClientId"]);
    }

    [Fact]
    public async Task GetToken_WithMissingApiKeyInRequest_ReturnsBadRequest()
    {
        // Arrange
        var client = _factory.CreateClient();
        var requestBody = new { ClientId = "testClient" };
        var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync("/api/v1/auth/token", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var responseString = await response.Content.ReadAsStringAsync();
        var problemDetails = JsonSerializer.Deserialize<ValidationProblemDetails>(responseString, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(problemDetails);
        Assert.Equal("One or more validation errors occurred.", problemDetails.Title);
        Assert.NotNull(problemDetails.Errors);
        Assert.True(problemDetails.Errors.ContainsKey("ApiKey"));
        Assert.Contains("API key is required.", problemDetails.Errors["ApiKey"]);
    }

    [Fact]
    public async Task GetToken_WithEmptyJsonBody_ReturnsBadRequest()
    {
        // Arrange
        var client = _factory.CreateClient();
        var content = new StringContent("{}", Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync("/api/v1/auth/token", content);
        
        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var responseString = await response.Content.ReadAsStringAsync();
        var problemDetails = JsonSerializer.Deserialize<ValidationProblemDetails>(responseString, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(problemDetails);
        Assert.Equal("One or more validation errors occurred.", problemDetails.Title);
        Assert.NotNull(problemDetails.Errors);
        Assert.True(problemDetails.Errors.ContainsKey("ClientId"));
        Assert.Contains("Client ID is required.", problemDetails.Errors["ClientId"]);
        Assert.True(problemDetails.Errors.ContainsKey("ApiKey"));
        Assert.Contains("API key is required.", problemDetails.Errors["ApiKey"]);
    }

    [Fact]
    public async Task GetToken_WithEmptyApiKey_ReturnsBadRequest()
    {
        // Arrange
        var client = _factory.CreateClient();
        var requestBody = new { ClientId = "testClient", ApiKey = "" };
        var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync("/api/v1/auth/token", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var responseString = await response.Content.ReadAsStringAsync();
        var problemDetails = JsonSerializer.Deserialize<ValidationProblemDetails>(responseString, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(problemDetails);
        Assert.Equal("One or more validation errors occurred.", problemDetails.Title);
        Assert.NotNull(problemDetails.Errors);
        Assert.True(problemDetails.Errors.ContainsKey("ApiKey"));
        Assert.Contains("API key is required.", problemDetails.Errors["ApiKey"]);
        Assert.Contains("API key must be at least 10 characters long.", problemDetails.Errors["ApiKey"]);
    }
    
    [Fact]
    public async Task GetToken_WithMalformedJsonBody_ReturnsBadRequest()
    {
        // Arrange
        var client = _factory.CreateClient();
        var malformedJson = "{\\\"clientId\\\": \\\"test\\\", \\\"apiKey\\\": \\\"malformed\\\"}"; 

        // Act
        var response = await client.PostAsync("/api/v1/auth/token", new StringContent(malformedJson, Encoding.UTF8, "application/json"));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problemDetails);
        Assert.Equal("One or more validation errors occurred.", problemDetails.Title);
    }

    [Fact]
    public async Task GetToken_NonAdminClient_ReturnsTokenWithOnlyUserRole()
    {
        // Arrange
        var loginRequest = new LoginRequest { ClientId = "regular_user_for_me", ApiKey = "demo_api_key" };

        // Act
        var (tokenResponseMsg, tokenData, _) = await PostAndDeserializeAsync<TokenResponseModel>("/api/v1/auth/token", loginRequest);
        
        // Assert
        Assert.Equal(HttpStatusCode.OK, tokenResponseMsg.StatusCode);
        Assert.NotNull(tokenData?.AccessToken);

        // Call /me endpoint to verify roles
        var (meResponseMsg, meContent, meProblemDetails) = await GetAndDeserializeAsync<MeResponseModel>("/api/v1/auth/me", tokenData.AccessToken);
        
        Assert.Equal(HttpStatusCode.OK, meResponseMsg.StatusCode);
        Assert.NotNull(meContent);
        Assert.NotNull(meContent.Roles);
        Assert.Contains("User", meContent.Roles);
        Assert.DoesNotContain("Admin", meContent.Roles);
        Assert.Null(meProblemDetails);
    }

    [Fact]
    public async Task GetToken_WithEmptyClientId_ReturnsBadRequest()
    {
        // Arrange
        var request = new LoginRequest { ClientId = "", ApiKey = "demo_api_key" };

        // Act
        var (response, _, problemDetails) = await PostAndDeserializeAsync<TokenResponseModel>("/api/v1/auth/token", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(problemDetails);
        Assert.Equal("One or more validation errors occurred.", problemDetails.Title);
    }

    [Fact]
    public async Task GetToken_WithNullRequestBody_ReturnsBadRequest()
    {
        // Arrange
        var client = _factory.CreateClient();
        var content = new StringContent("null", Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync("/api/v1/auth/token", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problemDetails);
        Assert.Equal("One or more validation errors occurred.", problemDetails.Title);
        Assert.Equal((int)HttpStatusCode.BadRequest, problemDetails.Status);
    }

    [Fact]
    public async Task GetCurrentUser_ValidToken_ReturnsOkAndUserInfo()
    {
        // Arrange
        var token = await GetValidTokenAsync("regular_user_for_me"); 
        Assert.NotNull(token);

        // Act
        var (response, meResponse, problemDetails) = await GetAndDeserializeAsync<MeResponseModel>("/api/v1/auth/me", token);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(meResponse);
        Assert.Equal("regular_user_for_me", meResponse.ClientId);
        Assert.NotNull(meResponse.Roles);
        Assert.Contains("User", meResponse.Roles);
        Assert.DoesNotContain("Admin", meResponse.Roles);
        Assert.True(meResponse.IsAuthenticated);
        Assert.False(string.IsNullOrWhiteSpace(meResponse.ExpiresAt));
        Assert.Null(problemDetails);
    }

    [Fact]
    public async Task GetCurrentUser_NoToken_ReturnsUnauthorized()
    {
        // Act
        var (response, _, problemDetails) = await GetAndDeserializeAsync<MeResponseModel>("/api/v1/auth/me");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotNull(problemDetails);
    }
    
    [Fact]
    public async Task GetCurrentUser_InvalidToken_ReturnsUnauthorized()
    {
        // Act
        var (response, _, problemDetails) = await GetAndDeserializeAsync<MeResponseModel>("/api/v1/auth/me", "this.is.an.invalid.token");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotNull(problemDetails);
    }

    public async Task<string?> GetValidTokenAsync(string clientId = "admin_client", string apiKey = "demo_api_key")
    {
        var request = new LoginRequest { ClientId = clientId, ApiKey = apiKey };
        var (response, tokenResponse, _) = await PostAndDeserializeAsync<TokenResponseModel>("/api/v1/auth/token", request);
        if (response.IsSuccessStatusCode && tokenResponse?.AccessToken != null)
        {
            return tokenResponse.AccessToken;
        }
        return null;
    }
}
