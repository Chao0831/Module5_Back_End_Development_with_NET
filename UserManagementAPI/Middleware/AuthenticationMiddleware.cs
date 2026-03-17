// Middleware/AuthenticationMiddleware.cs
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace UserManagementAPI.Middleware;

public class AuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AuthenticationMiddleware> _logger;
    private readonly JwtSettings _jwtSettings;

    public AuthenticationMiddleware(
        RequestDelegate next,
        ILogger<AuthenticationMiddleware> logger,
        IOptions<JwtSettings> jwtSettings)
    {
        _next = next;
        _logger = logger;
        _jwtSettings = jwtSettings.Value;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip authentication for certain paths (like login, swagger)
        if (IsPublicPath(context.Request.Path))
        {
            await _next(context);
            return;
        }

        // Get token from Authorization header
        var token = ExtractTokenFromHeader(context);

        if (string.IsNullOrEmpty(token))
        {
            _logger.LogWarning("No token provided for request to {Path}", context.Request.Path);
            await WriteUnauthorizedResponse(context, "No authentication token provided");
            return;
        }

        try
        {
            // Validate the token
            var principal = ValidateToken(token);

            if (principal == null)
            {
                _logger.LogWarning("Invalid token for request to {Path}", context.Request.Path);
                await WriteUnauthorizedResponse(context, "Invalid authentication token");
                return;
            }

            // Attach user to context
            context.User = principal;

            // Log successful authentication
            var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var username = principal.FindFirst(ClaimTypes.Name)?.Value;
            _logger.LogInformation("User {Username} (ID: {UserId}) authenticated successfully", username, userId);

            await _next(context);
        }
        catch (SecurityTokenExpiredException)
        {
            _logger.LogWarning("Expired token for request to {Path}", context.Request.Path);
            await WriteUnauthorizedResponse(context, "Authentication token has expired");
        }
        catch (SecurityTokenException ex)
        {
            _logger.LogWarning(ex, "Token validation failed for request to {Path}", context.Request.Path);
            await WriteUnauthorizedResponse(context, "Invalid authentication token");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during authentication for request to {Path}", context.Request.Path);
            await WriteUnauthorizedResponse(context, "Authentication failed");
        }
    }

    private bool IsPublicPath(PathString path)
    {
        var publicPaths = new[]
        {
            "/swagger",
            "/api/auth/login",
            "/api/auth/register",
            "/health",
            "/favicon.ico"
        };

        return publicPaths.Any(p => path.StartsWithSegments(p, StringComparison.OrdinalIgnoreCase));
    }

    private string? ExtractTokenFromHeader(HttpContext context)
    {
        var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
        
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return authHeader.Substring("Bearer ".Length).Trim();
    }

    private ClaimsPrincipal? ValidateToken(string token)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes(_jwtSettings.SecretKey);

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateIssuer = true,
            ValidIssuer = _jwtSettings.Issuer,
            ValidateAudience = true,
            ValidAudience = _jwtSettings.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };

        try
        {
            var principal = tokenHandler.ValidateToken(token, validationParameters, out _);
            return principal;
        }
        catch
        {
            return null;
        }
    }

    private static async Task WriteUnauthorizedResponse(HttpContext context, string message)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "application/json";

        var response = new
        {
            statusCode = 401,
            message,
            timestamp = DateTime.UtcNow
        };

        var jsonResponse = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(jsonResponse);
    }
}

// JWT Settings Configuration
public class JwtSettings
{
    public string SecretKey { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public int ExpirationMinutes { get; set; } = 60;
}