using System;

namespace CurrencyConverterApi.Infrastructure.Configuration
{
    /// <summary>
    /// Configuration settings for the Currency API
    /// </summary>
    public class CurrencyApiSettings
    {
        /// <summary>
        /// Base URL for the Frankfurter API
        /// </summary>
        public string FrankfurterApiBaseUrl { get; set; } = "https://api.frankfurter.app";
        
        /// <summary>
        /// Timeout in seconds for API calls
        /// </summary>
        public int ApiTimeoutSeconds { get; set; } = 30;
        
        /// <summary>
        /// Cache settings for different operations
        /// </summary>
        public CacheSettings Cache { get; set; } = new CacheSettings();
        
        /// <summary>
        /// Rate limiting settings
        /// </summary>
        public RateLimitSettings RateLimit { get; set; } = new RateLimitSettings();
        
        /// <summary>
        /// JWT authentication settings
        /// </summary>
        public JwtSettings Jwt { get; set; } = new JwtSettings();
        
        /// <summary>
        /// Retry policy settings
        /// </summary>
        public RetrySettings Retry { get; set; } = new RetrySettings();
    }
    
    public class CacheSettings
    {
        /// <summary>
        /// Expiration time in minutes for latest rates cache
        /// </summary>
        public int LatestRatesExpirationMinutes { get; set; } = 30;
        
        /// <summary>
        /// Expiration time in minutes for conversion cache
        /// </summary>
        public int ConversionExpirationMinutes { get; set; } = 30;
        
        /// <summary>
        /// Expiration time in hours for historical rates cache
        /// </summary>
        public int HistoricalRatesExpirationHours { get; set; } = 6;
    }
    
    public class RateLimitSettings
    {
        /// <summary>
        /// Maximum number of requests per client within the specified period
        /// </summary>
        public int TokenLimit { get; set; } = 100;
        
        /// <summary>
        /// Period in minutes for replenishing tokens
        /// </summary>
        public int ReplenishmentPeriodMinutes { get; set; } = 1;
        
        /// <summary>
        /// Number of tokens replenished per period
        /// </summary>
        public int TokensPerPeriod { get; set; } = 100;
    }
    
    public class JwtSettings
    {
        /// <summary>
        /// Secret key for JWT token signing
        /// </summary>
        public string SecretKey { get; set; } = "default_secret_key_change_in_production";
        
        /// <summary>
        /// Token expiration time in minutes
        /// </summary>
        public int ExpirationMinutes { get; set; } = 60;
        
        /// <summary>
        /// Token issuer
        /// </summary>
        public string Issuer { get; set; } = "CurrencyConverterApi";
        
        /// <summary>
        /// Token audience
        /// </summary>
        public string Audience { get; set; } = "CurrencyApiUsers";
        
        /// <summary>
        /// API key for authentication
        /// </summary>
        public string ApiKey { get; set; } = "demo_api_key";
    }
    
    public class RetrySettings
    {
        /// <summary>
        /// Maximum number of retry attempts
        /// </summary>
        public int MaxRetryAttempts { get; set; } = 3;
        
        /// <summary>
        /// Circuit breaker failure threshold
        /// </summary>
        public int CircuitBreakerThreshold { get; set; } = 5;
        
        /// <summary>
        /// Circuit breaker duration in minutes
        /// </summary>
        public int CircuitBreakerDurationMinutes { get; set; } = 1;
    }
}