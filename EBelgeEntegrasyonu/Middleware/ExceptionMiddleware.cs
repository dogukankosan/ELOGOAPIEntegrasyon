using System.Net;
using System.Text.Json;
using EBelgeAPI.Data.Interfaces;
using EBelgeAPI.Models.Entities;
using EBelgeAPI.Models.Responses;

namespace EBelgeAPI.Middleware;

public class ExceptionMiddleware(RequestDelegate next, IServiceScopeFactory scopeFactory)
{
    private static readonly JsonSerializerOptions _jsonOpt = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }
    private async Task HandleExceptionAsync(HttpContext context, Exception ex)
    {
        // ── Log yaz ──
        try
        {
            using var scope = scopeFactory.CreateScope();
            var logRepo = scope.ServiceProvider.GetRequiredService<IApiLogRepository>();
            await logRepo.WriteAsync(new ApiLog
            {
                Level = "ERROR",
                Source = "API",
                Method = context.Request.Method,
                Path = context.Request.Path,
                StatusCode = 500,
                Message = $"Yakalanmamış hata: {ex.Message}",
                Detail = ex.ToString(),
                Username = context.User?.Identity?.Name,
                IpAddress = context.Connection.RemoteIpAddress?.ToString(),
                CreatedAt = DateTime.Now
            });
        }
        catch { /* Log yazılamazsa bile response dönmeli */ }
        // ── Response dön ──
        if (context.Response.HasStarted) return;
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
        var response = ApiResponse<object>.Fail(
            "Sunucuda beklenmedik bir hata oluştu. Lütfen daha sonra tekrar deneyin.");
        await context.Response.WriteAsync(
            JsonSerializer.Serialize(response, _jsonOpt));
    }
}