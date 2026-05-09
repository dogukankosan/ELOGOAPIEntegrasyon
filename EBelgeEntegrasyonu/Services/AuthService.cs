using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using EBelgeAPI.Data.Interfaces;
using EBelgeAPI.Models.Entities;
using EBelgeAPI.Models.Requests;
using EBelgeAPI.Models.Responses;
using EBelgeAPI.Services.Interfaces;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;

namespace EBelgeAPI.Services;

public class AuthService(
    ILogoSettingsRepository settingsRepo,
    IHttpClientFactory httpClientFactory,
    ILogService log,
    IConfiguration config) : IAuthService
{
    public async Task<(bool Success, LoginResponse? Response, string? Error)>
        LoginAsync(LoginRequest request)
    {
        LogoSettings? settings = await settingsRepo.GetActiveSettingsAsync();
        if (settings == null)
            return (false, null, "Sistem ayarları bulunamadı. Lütfen yöneticinizle iletişime geçin.");
        var (ok, error) = await ValidateWithLogoAsync(settings, request.Username, request.Password);
        if (!ok)
        {
            await log.WarningAsync(
                $"Başarısız giriş denemesi. Kullanıcı: {request.Username}",
                path: "/api/auth/login", method: "POST", statusCode: 401,
                username: request.Username);
            return (false, null, error);
        }
        var expiresAt = DateTime.UtcNow.AddHours(config.GetValue<int>("Jwt:ExpiresHours", 8));
        string? token = GenerateJwt(request.Username, expiresAt);
        return (true, new LoginResponse
        {
            Token = token,
            FullName = request.Username,
            ExpiresAt = expiresAt
        }, null);
    }
    private async Task<(bool Ok, string? Error)> ValidateWithLogoAsync(
       LogoSettings settings, string username, string password)
    {
        try
        {
            HttpClient? client = httpClientFactory.CreateClient("Logo");
            string? url = "https://idm.logo.cloud/legacy/sts/api/oauth/token";
            Dictionary<string, string> form = new Dictionary<string, string>
            {
                ["grant_type"] = "password",
                ["client_id"] = settings.ClientId,
                ["client_secret"] = settings.ClientSecret,
                ["customer_id"] = settings.CustomerId,
                ["username"] = username,
                ["password"] = password,
                ["firm"] = settings.FirmNr.Trim()
            };
            HttpResponseMessage resp = await client.PostAsync(url, new FormUrlEncodedContent(form));
            string? body = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
            {
                try
                {
                    dynamic? err = JsonConvert.DeserializeObject(body);
                    string? desc = err?.error_description;
                    if (!string.IsNullOrEmpty(desc))
                        return (false, $"Logo kimlik doğrulama hatası: {desc}");
                }
                catch { }
                return (false, $"Logo'dan {(int)resp.StatusCode} döndü: {body}");
            }
            return (true, null);
        }
        catch (HttpRequestException)
        {
            return (false, "Logo sunucusuna bağlanılamadı. Lütfen internet bağlantınızı kontrol edin.");
        }
        catch (TaskCanceledException)
        {
            return (false, "Logo sunucusu yanıt vermedi. Lütfen daha sonra tekrar deneyin.");
        }
        catch (Exception ex)
        {
            await log.ErrorAsync("Logo doğrulama sırasında beklenmedik hata.",
                detail: ex.ToString(), source: "System");
            return (false, "Kimlik doğrulama sırasında beklenmedik bir hata oluştu.");
        }
    }
    private string GenerateJwt(string username, DateTime expiresAt)
    {
        SymmetricSecurityKey key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Secret"]!));
        SigningCredentials creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub,        username),
            new Claim(JwtRegisteredClaimNames.UniqueName, username),
            new Claim(JwtRegisteredClaimNames.Jti,        Guid.NewGuid().ToString())
        };
        JwtSecurityToken token = new JwtSecurityToken(
            issuer: config["Jwt:Issuer"],
            audience: config["Jwt:Audience"],
            claims: claims,
            expires: expiresAt,
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}