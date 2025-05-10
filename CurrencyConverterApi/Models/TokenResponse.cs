using System;

namespace CurrencyConverterApi.Models;

/// <summary>
/// Response model for authentication tokens
/// </summary>
public class TokenResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string TokenType { get; set; } = "Bearer";
    public int ExpiresIn { get; set; }
    public DateTime IssuedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}

/// <summary>
/// Login model for authentication requests
/// </summary>
public class LoginRequest
{
    public string? ApiKey { get; set; }
    public string ClientId { get; set; } = string.Empty;
}
