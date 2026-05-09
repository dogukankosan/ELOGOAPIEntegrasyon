using System.Text.Json;
using EBelgeAPI.Models.Responses;

namespace EBelgeAPI.Middleware;

public class ApiKeyMiddleware(RequestDelegate next, IConfiguration config)
{
    private const string ApiKeyHeader = "X-API-Key";
    public async Task InvokeAsync(HttpContext context)
    {
        // Swagger'a izin ver
        if (context.Request.Path.StartsWithSegments("/swagger"))
        {
            await next(context);
            return;
        }

        // Settings endpoint'ine izin ver — kendi key'i var
        if (context.Request.Path.StartsWithSegments("/api/settings"))
        {
            await next(context);
            return;
        }
        string? expectedKey = config["ApiKey"];
        if (string.IsNullOrEmpty(expectedKey))
        {
            await next(context);
            return;
        }
        if (!context.Request.Headers.TryGetValue(ApiKeyHeader, out var receivedKey)
            || receivedKey != expectedKey)
        {
            context.Response.StatusCode = 401;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(
                ApiResponse<object>.Fail("Geçersiz veya eksik API anahtarı."),
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
            return;
        }
        await next(context);
    }
}