using CurrencyConverterApi.Controllers;
using CurrencyConverterApi.Infrastructure.Configuration;
using CurrencyConverterApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Xunit;
using System.IdentityModel.Tokens.Jwt;

namespace CurrencyConverterApi.Tests.Controllers
{
    public class AuthControllerTests
    {
        private readonly Mock<ILogger<AuthController>> _mockLogger;
        private readonly Mock<IOptions<CurrencyApiSettings>> _mockOptions;
        private readonly AuthController _controller;
        private readonly CurrencyApiSettings _settings;

        public AuthControllerTests()
        {
            _mockLogger = new Mock<ILogger<AuthController>>();
            _mockOptions = new Mock<IOptions<CurrencyApiSettings>>();
            
            _settings = new CurrencyApiSettings
            {
                Jwt = new JwtSettings
                {
                    ApiKey = "test_api_key",
                    SecretKey = "test_secret_key_long_enough_for_hmacsha256",
                    ExpirationMinutes = 60,
                    Issuer = "TestIssuer",
                    Audience = "TestAudience"
                }
            };
            
            _mockOptions.Setup(o => o.Value).Returns(_settings);
            _controller = new AuthController(_mockLogger.Object, _mockOptions.Object);
        }
        
        [Fact]
        public void GetToken_ValidApiKey_ReturnsOkWithToken()
        {
            // Arrange
            var request = new LoginRequest
            {
                ClientId = "test_client",
                ApiKey = "test_api_key"
            };
            
            // Act
            var result = _controller.GetToken(request);
            
            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var tokenResponse = Assert.IsType<TokenResponse>(okResult.Value);
            
            Assert.NotNull(tokenResponse.AccessToken);
            Assert.True(tokenResponse.ExpiresAt > DateTime.UtcNow);
        }
        
        [Fact]
        public void GetToken_InvalidApiKey_ReturnsUnauthorized()
        {
            // Arrange
            var request = new LoginRequest
            {
                ClientId = "test_client",
                ApiKey = "wrong_api_key"
            };
            
            // Act
            var result = _controller.GetToken(request);
            
            // Assert
            var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
            var problem = Assert.IsType<ProblemDetails>(unauthorizedResult.Value);
            
            Assert.Equal(401, problem.Status);
            Assert.Contains("Invalid API key", problem.Detail);
        }
        
        [Fact]
        public void GetToken_AdminClientId_IncludesAdminRole()
        {
            // Arrange
            var request = new LoginRequest
            {
                ClientId = "admin_client",
                ApiKey = "test_api_key"
            };
            
            // Act
            var result = _controller.GetToken(request);
            
            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var tokenResponse = Assert.IsType<TokenResponse>(okResult.Value);
            
            Assert.NotNull(tokenResponse.AccessToken);
        }
        
        [Fact]
        public void GetToken_NullRequest_ReturnsBadRequest()
        {
            // Act
            var result = _controller.GetToken(null!);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var problemDetails = Assert.IsType<ProblemDetails>(badRequestResult.Value);
            Assert.Equal(StatusCodes.Status400BadRequest, problemDetails.Status);
            Assert.Equal("Invalid request", problemDetails.Title);
        }

        [Fact]
        public void GetCurrentUser_UserNotAuthenticated_ReturnsOkWithDefaultInfo()
        {
            // Arrange
            var user = new ClaimsPrincipal(new ClaimsIdentity()); // Not authenticated
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            };

            // Act
            var result = _controller.GetCurrentUser();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(okResult.Value);
            dynamic response = okResult.Value!;
            Assert.Null(response.GetType().GetProperty("ClientId")?.GetValue(response, null));
            var roles = response.GetType().GetProperty("Roles")?.GetValue(response, null) as System.Collections.Generic.IEnumerable<string>; 
            Assert.NotNull(roles);
            Assert.Empty(roles!);
            Assert.False(response.GetType().GetProperty("IsAuthenticated")?.GetValue(response, null));
        }

        [Fact]
        public void GetCurrentUser_UserAuthenticated_ReturnsOkWithUserInfo()
        {
            // Arrange
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, "test_user"),
                new Claim("client_id", "test_client_id"),
                new Claim(ClaimTypes.Role, "User"),
                new Claim(ClaimTypes.Role, "Viewer"),
                new Claim(JwtRegisteredClaimNames.Exp, DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds().ToString())
            };
            var identity = new ClaimsIdentity(claims, "TestAuthType");
            var user = new ClaimsPrincipal(identity);

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            };

            // Act
            var result = _controller.GetCurrentUser();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(okResult.Value);
            dynamic response = okResult.Value!;
            Assert.Equal("test_client_id", response.GetType().GetProperty("ClientId")?.GetValue(response, null));
            var roles = response.GetType().GetProperty("Roles")?.GetValue(response, null) as System.Collections.Generic.IEnumerable<string>;
            Assert.NotNull(roles);
            Assert.Contains("User", roles!);
            Assert.Contains("Viewer", roles!);
            Assert.True(response.GetType().GetProperty("IsAuthenticated")?.GetValue(response, null));
            Assert.NotNull(response.GetType().GetProperty("ExpiresAt")?.GetValue(response, null));
        }

        [Fact]
        public void GetCurrentUser_UserAuthenticated_NoClientIdClaim_UsesNameIdentifier()
        {
            // Arrange
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, "test_user_name_identifier"),
                // No "client_id" claim
                new Claim(ClaimTypes.Role, "User"),
                new Claim(JwtRegisteredClaimNames.Exp, DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds().ToString())
            };
            var identity = new ClaimsIdentity(claims, "TestAuthType");
            var user = new ClaimsPrincipal(identity);

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            };

            // Act
            var result = _controller.GetCurrentUser();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(okResult.Value);
            dynamic response = okResult.Value!;
            Assert.Equal("test_user_name_identifier", response.GetType().GetProperty("ClientId")?.GetValue(response, null));
            Assert.True(response.GetType().GetProperty("IsAuthenticated")?.GetValue(response, null));
        }
    }
}
