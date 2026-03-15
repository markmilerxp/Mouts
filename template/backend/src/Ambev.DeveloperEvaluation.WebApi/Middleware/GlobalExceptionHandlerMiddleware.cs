using Ambev.DeveloperEvaluation.WebApi.Common;
using System.Text.Json;

namespace Ambev.DeveloperEvaluation.WebApi.Middleware;

public class GlobalExceptionHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;

    public GlobalExceptionHandlerMiddleware(RequestDelegate next, ILogger<GlobalExceptionHandlerMiddleware> logger)
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
            _logger.LogError(ex, "Unhandled exception caught by GlobalExceptionHandlerMiddleware");
            await HandleExceptionAsync(context, ex);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var (statusCode, response) = exception switch
        {
            KeyNotFoundException ex => (
                StatusCodes.Status404NotFound,
                new ApiErrorResponse
                {
                    Type = "ResourceNotFound",
                    Error = "Resource not found",
                    Detail = ex.Message
                }),

            InvalidOperationException ex => (
                StatusCodes.Status400BadRequest,
                new ApiErrorResponse
                {
                    Type = "BusinessError",
                    Error = "Business rule violation",
                    Detail = ex.Message
                }),

            DomainException ex => (
                StatusCodes.Status400BadRequest,
                new ApiErrorResponse
                {
                    Type = "DomainError",
                    Error = "Domain rule violation",
                    Detail = ex.Message
                }),

            UnauthorizedAccessException ex => (
                StatusCodes.Status401Unauthorized,
                new ApiErrorResponse
                {
                    Type = "AuthenticationError",
                    Error = "Unauthorized",
                    Detail = ex.Message
                }),

            _ => (
                StatusCodes.Status500InternalServerError,
                new ApiErrorResponse
                {
                    Type = "InternalError",
                    Error = "An unexpected error occurred",
                    Detail = "Please contact support if the problem persists."
                })
        };

        context.Response.StatusCode = statusCode;

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(response, jsonOptions));
    }
}
