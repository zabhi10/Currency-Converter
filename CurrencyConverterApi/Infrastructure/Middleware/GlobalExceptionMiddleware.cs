using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using CurrencyConverterApi.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace CurrencyConverterApi.Infrastructure.Middleware
{
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionMiddleware> _logger;

        public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unhandled exception occurred while processing the request for {Path}", context.Request.Path);
                await HandleExceptionAsync(context, ex);
            }
        }

        private static Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            context.Response.ContentType = "application/problem+json";
            var problemDetails = new ProblemDetails
            {
                Instance = context.Request.Path
            };

            switch (exception)
            {
                case BadRequestException badRequestException:
                    problemDetails.Title = badRequestException.Message;
                    problemDetails.Status = (int)HttpStatusCode.BadRequest;
                    break;
                case HttpRequestException httpRequestException:
                    _ = httpRequestException;
                    problemDetails.Title = "External service error.";
                    problemDetails.Status = (int)HttpStatusCode.ServiceUnavailable;
                    problemDetails.Detail = "An error occurred while communicating with an external service. Please try again later.";
                    break;
                default:
                    problemDetails.Title = "An unexpected error occurred.";
                    problemDetails.Status = (int)HttpStatusCode.InternalServerError;
                    problemDetails.Detail = "An unexpected error occurred. Please try again later.";
                    break;
            }

            context.Response.StatusCode = problemDetails.Status.Value;
            var result = JsonSerializer.Serialize(problemDetails);
            return context.Response.WriteAsync(result);
        }
    }
}

// Extension method used to add the middleware to the HTTP request pipeline.
namespace Microsoft.AspNetCore.Builder
{
    public static class GlobalExceptionMiddlewareExtensions
    {
        public static IApplicationBuilder UseGlobalExceptionMiddleware(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<CurrencyConverterApi.Infrastructure.Middleware.GlobalExceptionMiddleware>();
        }
    }
}
