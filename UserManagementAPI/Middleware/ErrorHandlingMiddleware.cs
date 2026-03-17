// Middleware/ErrorHandlingMiddleware.cs
using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace UserManagementAPI.Middleware;

public class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;
    private readonly IWebHostEnvironment _env;

    public ErrorHandlingMiddleware(
        RequestDelegate next, 
        ILogger<ErrorHandlingMiddleware> logger,
        IWebHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        _logger.LogError(exception, "An error occurred while processing request {Path}", context.Request.Path);

        var response = context.Response;
        response.ContentType = "application/json";

        var errorResponse = new ErrorResponse
        {
            Timestamp = DateTime.UtcNow,
            Path = context.Request.Path,
            Method = context.Request.Method
        };

        switch (exception)
        {
            case UnauthorizedAccessException:
                response.StatusCode = (int)HttpStatusCode.Unauthorized;
                errorResponse.StatusCode = response.StatusCode;
                errorResponse.Message = "You are not authorized to access this resource.";
                errorResponse.Type = "Unauthorized";
                break;

            case KeyNotFoundException:
                response.StatusCode = (int)HttpStatusCode.NotFound;
                errorResponse.StatusCode = response.StatusCode;
                errorResponse.Message = exception.Message;
                errorResponse.Type = "NotFound";
                break;

            case ArgumentException:
            case ValidationException:
                response.StatusCode = (int)HttpStatusCode.BadRequest;
                errorResponse.StatusCode = response.StatusCode;
                errorResponse.Message = exception.Message;
                errorResponse.Type = "BadRequest";
                break;

            case DbUpdateConcurrencyException:
                response.StatusCode = (int)HttpStatusCode.Conflict;
                errorResponse.StatusCode = response.StatusCode;
                errorResponse.Message = "The record you attempted to update was modified by another user.";
                errorResponse.Type = "ConcurrencyConflict";
                break;

            case DbUpdateException:
                response.StatusCode = (int)HttpStatusCode.Conflict;
                errorResponse.StatusCode = response.StatusCode;
                errorResponse.Message = "A database error occurred while processing your request.";
                errorResponse.Type = "DatabaseError";
                break;

            default:
                response.StatusCode = (int)HttpStatusCode.InternalServerError;
                errorResponse.StatusCode = response.StatusCode;
                errorResponse.Message = "An internal server error occurred. Please try again later.";
                errorResponse.Type = "InternalServerError";
                
                // Include detailed error information only in development
                if (_env.IsDevelopment())
                {
                    errorResponse.DetailedMessage = exception.Message;
                    errorResponse.StackTrace = exception.StackTrace;
                }
                break;
        }

        var jsonResponse = JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await response.WriteAsync(jsonResponse);
    }
}

public class ErrorResponse
{
    public int StatusCode { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string? DetailedMessage { get; set; }
    public string? StackTrace { get; set; }
}

// Custom Validation Exception
public class ValidationException : Exception
{
    public ValidationException(string message) : base(message)
    {
    }
}