using System.IdentityModel.Tokens.Jwt;
using EBelgeAPI.Data.Interfaces;
using EBelgeAPI.Models.Requests;
using EBelgeAPI.Models.Responses;
using EBelgeAPI.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EBelgeAPI.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(IAuthService authService) : ControllerBase
{
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username))
            return BadRequest(ApiResponse<LoginResponse>.Fail("Kullanıcı adı boş olamaz."));
        if (string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(ApiResponse<LoginResponse>.Fail("Şifre boş olamaz."));
        var (success, response, error) = await authService.LoginAsync(request);
        if (!success)
            return Unauthorized(ApiResponse<LoginResponse>.Fail(error!));
        return Ok(ApiResponse<LoginResponse>.Ok(response!,
            "Giriş başarılı. Token bilgilerini güvenli saklayın."));
    }
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(
        [FromServices] IRevokedTokenRepository revokedTokenRepo)
    {
        string? authHeader = Request.Headers["Authorization"].FirstOrDefault();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            return BadRequest(ApiResponse<object>.Fail("Token bulunamadı."));
        string? tokenStr = authHeader["Bearer ".Length..].Trim();
        JwtSecurityTokenHandler handler = new JwtSecurityTokenHandler();
        JwtSecurityToken jwt = handler.ReadJwtToken(tokenStr);
        await revokedTokenRepo.RevokeAsync(
            jti: jwt.Id,
            username: User.Identity?.Name ?? "",
            expiresAt: jwt.ValidTo);
        await revokedTokenRepo.CleanupExpiredAsync();
        return Ok(ApiResponse<object>.Ok(null, "Oturum başarıyla sonlandırıldı."));
    }
}