using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using CurrencyConverterApi.Infrastructure.Configuration;
using CurrencyConverterApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CurrencyConverterApi.Controllers
{
    [ApiController]
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1.0")]
    public class AuthController : ControllerBase
    {
        private readonly ILogger<AuthController> _logger;
        private readonly CurrencyApiSettings _settings;

        public AuthController(
            ILogger<AuthController> logger,
            IOptions<CurrencyApiSettings> settings)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
        }
        /// <summary>
        /// Get a JWT token for accessing the API
        /// </summary>
        /// <param name="request">Login credentials</param>
        /// <returns>JWT token response</returns>
        [HttpPost("token")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public IActionResult GetToken([FromBody] LoginRequest request)
        {
            _logger.LogInformation("Attempting to generate token for ClientId: {ClientId}", request?.ClientId);

            if (request == null)
            {
                _logger.LogWarning("Login request body is null or could not be bound.");
                return BadRequest(new ProblemDetails
                {
                    Title = "Invalid request",
                    Detail = "The request body is missing or malformed.",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            if (request.ApiKey != _settings.Jwt.ApiKey)
            {
                _logger.LogWarning("Invalid API key attempt from client {ClientId}", request.ClientId);
                return Unauthorized(new ProblemDetails { Title = "Authentication Failed", Detail = "Invalid API key", Status = StatusCodes.Status401Unauthorized });
            }

            var issuer = _settings.Jwt?.Issuer ?? "CurrencyConverterApi";
            var audience = _settings.Jwt?.Audience ?? "CurrencyApiUsers";
            var secretKey = _settings.Jwt?.SecretKey ?? "default_key_for_development_only";
            var expirationMinutes = _settings.Jwt?.ExpirationMinutes ?? 60;

            // Create claims for the token
            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, request.ClientId),
                new Claim("client_id", request.ClientId),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.Role, "User")  // Default role
            };

            // For admin users (in a real app this would come from a database or auth server)
            if (request.ClientId.ToLower().Contains("admin"))
            {
                claims.Add(new Claim(ClaimTypes.Role, "Admin"));
            }

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var now = DateTime.UtcNow;
            var expires = now.AddMinutes(expirationMinutes);

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                notBefore: now,
                expires: expires,
                signingCredentials: creds);

            var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

            _logger.LogInformation("JWT token generated for client {ClientId}", request.ClientId);

            return Ok(new TokenResponse
            {
                AccessToken = tokenString,
                ExpiresIn = expirationMinutes * 60,  // Return in seconds
                IssuedAt = now,
                ExpiresAt = expires
            });
        }

        /// <summary>
        /// Check if the current token is valid and display user information
        /// </summary>
        [HttpGet("me")]
        [Authorize]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        public IActionResult GetCurrentUser()
        {
            var clientId = User.FindFirstValue("client_id") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
            var roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value);

            return Ok(new
            {
                ClientId = clientId,
                Roles = roles,
                IsAuthenticated = User.Identity?.IsAuthenticated ?? false,
                ExpiresAt = User.FindFirstValue(JwtRegisteredClaimNames.Exp)
            });
        }
    }
}
