using System.Net;
using System.Text.Json;
using CurrencyConverterApi.Exceptions;
using CurrencyConverterApi.Infrastructure.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CurrencyConverterApi.Tests.Infrastructure.Middleware
{
    public class GlobalExceptionMiddlewareTests
    {
        private readonly Mock<RequestDelegate> _mockNext;
        private readonly Mock<ILogger<GlobalExceptionMiddleware>> _mockLogger;
        private readonly GlobalExceptionMiddleware _middleware;
        private DefaultHttpContext? _httpContext;

        public GlobalExceptionMiddlewareTests()
        {
            _mockNext = new Mock<RequestDelegate>();
            _mockLogger = new Mock<ILogger<GlobalExceptionMiddleware>>();
            _middleware = new GlobalExceptionMiddleware(_mockNext.Object, _mockLogger.Object);
        }

        private void SetupHttpContext()
        {
            _httpContext = new DefaultHttpContext();
            _httpContext.Response.Body = new MemoryStream();
            _httpContext.Request.Path = "/testpath"; 
        }

        [Fact]
        public async Task InvokeAsync_CallsNext_WhenNoException()
        {
            // Arrange
            SetupHttpContext();
            _mockNext.Setup(next => next(It.IsAny<HttpContext>())).Returns(Task.CompletedTask);

            // Act
            await _middleware.InvokeAsync(_httpContext!);

            // Assert
            _mockNext.Verify(next => next(_httpContext!), Times.Once);
            Assert.Equal((int)HttpStatusCode.OK, _httpContext!.Response.StatusCode);
        }

        [Fact]
        public async Task InvokeAsync_HandlesGenericException_ReturnsProblemDetails()
        {
            // Arrange
            SetupHttpContext();
            var exceptionMessage = "Test generic exception";
            var exception = new InvalidOperationException(exceptionMessage);
            _mockNext.Setup(next => next(It.IsAny<HttpContext>())).ThrowsAsync(exception);

            // Act
            await _middleware.InvokeAsync(_httpContext!);

            // Assert
            _httpContext!.Response.Body.Seek(0, SeekOrigin.Begin);
            var reader = new StreamReader(_httpContext.Response.Body);
            var responseBody = await reader.ReadToEndAsync();
            var problemDetails = JsonSerializer.Deserialize<ProblemDetails>(responseBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            Assert.NotNull(problemDetails);
            Assert.Equal((int)HttpStatusCode.InternalServerError, _httpContext.Response.StatusCode);
            Assert.Equal("An unexpected error occurred.", problemDetails.Title);
            Assert.Equal("An unexpected error occurred. Please try again later.", problemDetails.Detail);
            Assert.Equal("/testpath", problemDetails.Instance);

            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"An unhandled exception occurred while processing the request for {_httpContext.Request.Path}")),
                    exception,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task InvokeAsync_HandlesBadRequestException_ReturnsProblemDetails()
        {
            // Arrange
            SetupHttpContext();
            var exceptionMessage = "Test bad request";
            var exception = new BadRequestException(exceptionMessage);
            _mockNext.Setup(next => next(It.IsAny<HttpContext>())).ThrowsAsync(exception);

            // Act
            await _middleware.InvokeAsync(_httpContext!);

            // Assert
            _httpContext!.Response.Body.Seek(0, SeekOrigin.Begin);
            var reader = new StreamReader(_httpContext.Response.Body);
            var responseBody = await reader.ReadToEndAsync();
            var problemDetails = JsonSerializer.Deserialize<ProblemDetails>(responseBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            
            Assert.NotNull(problemDetails);
            Assert.Equal((int)HttpStatusCode.BadRequest, _httpContext.Response.StatusCode);
            Assert.Equal(exceptionMessage, problemDetails.Title);
            Assert.Null(problemDetails.Detail);
            Assert.Equal("/testpath", problemDetails.Instance);

            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error, 
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"An unhandled exception occurred while processing the request for {_httpContext.Request.Path}")),
                    exception,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task InvokeAsync_HandlesHttpRequestException_ReturnsProblemDetails()
        {
            // Arrange
            SetupHttpContext();
            var exceptionMessage = "Test HTTP request exception";
            var exception = new HttpRequestException(exceptionMessage);
            _mockNext.Setup(next => next(It.IsAny<HttpContext>())).ThrowsAsync(exception);

            // Act
            await _middleware.InvokeAsync(_httpContext!);

            // Assert
            _httpContext!.Response.Body.Seek(0, SeekOrigin.Begin);
            var reader = new StreamReader(_httpContext.Response.Body);
            var responseBody = await reader.ReadToEndAsync();
            var problemDetails = JsonSerializer.Deserialize<ProblemDetails>(responseBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            Assert.NotNull(problemDetails);
            Assert.Equal((int)HttpStatusCode.ServiceUnavailable, _httpContext.Response.StatusCode);
            Assert.Equal("External service error.", problemDetails.Title);
            Assert.Equal("An error occurred while communicating with an external service. Please try again later.", problemDetails.Detail);
            Assert.Equal("/testpath", problemDetails.Instance);
            
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error, 
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"An unhandled exception occurred while processing the request for {_httpContext.Request.Path}")),
                    exception,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }
    }
}
