using System.Net.Http.Headers;
using System.Text.Json;

namespace EBelgeUI.Middleware;
public class ExceptionMiddleware(
    RequestDelegate next,
    ILogger<ExceptionMiddleware> logger,
    IHttpClientFactory httpClientFactory)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "UI yakalanmamış hata. Path: {Path} User: {User}",
                context.Request.Path,
                context.User?.Identity?.Name ?? "anonim");
            // ── API'ye log at ──
            try
            {
                HttpClient client = httpClientFactory.CreateClient("API");
                string? token = context.Session.GetString("Token");
                if (!string.IsNullOrEmpty(token))
                    client.DefaultRequestHeaders.Authorization =
                        new AuthenticationHeaderValue("Bearer", token);
                string? logPayload = JsonSerializer.Serialize(new
                {
                    level = "ERROR",
                    source = "UI",
                    method = context.Request.Method,
                    path = context.Request.Path.ToString(),
                    statusCode = 500,
                    message = $"UI yakalanmamış hata: {ex.Message}",
                    detail = ex.ToString(),
                    username = context.User?.Identity?.Name ?? "anonim",
                    ipAddress = context.Connection.RemoteIpAddress?.ToString(),
                    createdAt = DateTime.Now
                });
                await client.PostAsync("/api/log/write",
                    new StringContent(logPayload, System.Text.Encoding.UTF8, "application/json"));
            }
            catch { /* log atılamazsa sessizce geç */ }
            if (context.Response.HasStarted) return;
            context.Response.StatusCode = 500;
            // AJAX mi?
            if (context.Request.Headers["X-Requested-With"] == "XMLHttpRequest"
                || context.Request.Headers["Accept"].ToString().Contains("application/json"))
            {
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(JsonSerializer.Serialize(new
                {
                    success = false,
                    message = "Sunucu hatası oluştu."
                }));
            }
            else
                context.Response.Redirect("/Dashboard/Error");
        }
    }
}