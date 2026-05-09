using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using EBelgeAPI.Data.Interfaces;
using EBelgeAPI.Models.Responses;

namespace EBelgeAPI.Middleware;

public class TokenRevocationMiddleware(RequestDelegate next, IServiceScopeFactory scopeFactory)
{
    public async Task InvokeAsync(HttpContext context)
    {
        string? authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
        if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
        {
            string? token = authHeader["Bearer ".Length..].Trim();
            try
            {
                JwtSecurityTokenHandler handler = new JwtSecurityTokenHandler();
                JwtSecurityToken jwt = handler.ReadJwtToken(token);
                string? jti = jwt.Id;
                if (!string.IsNullOrEmpty(jti))
                {
                    using var scope = scopeFactory.CreateScope();
                    var repo = scope.ServiceProvider.GetRequiredService<IRevokedTokenRepository>();
                    Boolean isRevoked = await repo.IsRevokedAsync(jti);
                    if (isRevoked)
                    {
                        context.Response.StatusCode = 401;
                        context.Response.ContentType = "application/json";
                        await context.Response.WriteAsync(JsonSerializer.Serialize(
                            ApiResponse<object>.Fail("Oturum sonlandırılmış. Lütfen tekrar giriş yapın."),
                            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
                        return;
                    }
                }
            }
            catch { }
        }
        await next(context);
    }
}