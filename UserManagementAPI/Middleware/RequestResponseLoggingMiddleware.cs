// Middleware/RequestResponseLoggingMiddleware.cs
using System.Diagnostics;
using System.Text;

namespace UserManagementAPI.Middleware;

public class RequestResponseLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestResponseLoggingMiddleware> _logger;

    public RequestResponseLoggingMiddleware(RequestDelegate next, ILogger<RequestResponseLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Log the incoming request
        await LogRequest(context);

        // Capture the original response body stream
        var originalBodyStream = context.Response.Body;
        using var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Call the next middleware in the pipeline
            await _next(context);

            stopwatch.Stop();

            // Log the outgoing response
            await LogResponse(context, stopwatch.ElapsedMilliseconds);
        }
        finally
        {
            // Copy the contents of the new memory stream to the original stream
            responseBody.Seek(0, SeekOrigin.Begin);
            await responseBody.CopyToAsync(originalBodyStream);
            context.Response.Body = originalBodyStream;
        }
    }

    private async Task LogRequest(HttpContext context)
    {
        context.Request.EnableBuffering();

        var requestBody = await ReadRequestBody(context.Request);

        _logger.LogInformation("Incoming Request - {Method} {Path} {QueryString} - IP: {IP} - Body: {Body}",
            context.Request.Method,
            context.Request.Path,
            context.Request.QueryString,
            context.Connection.RemoteIpAddress,
            requestBody);

        context.Request.Body.Position = 0; // Reset the stream position
    }

    private async Task LogResponse(HttpContext context, long elapsedMilliseconds)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var responseBody = await new StreamReader(context.Response.Body).ReadToEndAsync();
        context.Response.Body.Seek(0, SeekOrigin.Begin);

        _logger.LogInformation("Outgoing Response - {StatusCode} - {Path} - Duration: {Duration}ms - Body: {Body}",
            context.Response.StatusCode,
            context.Request.Path,
            elapsedMilliseconds,
            responseBody);
    }

    private static async Task<string> ReadRequestBody(HttpRequest request)
    {
        if (request.Body == null || !request.Body.CanRead)
        {
            return string.Empty;
        }

        using var reader = new StreamReader(
            request.Body,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: false,
            bufferSize: 1024,
            leaveOpen: true);

        var body = await reader.ReadToEndAsync();
        return body;
    }
}