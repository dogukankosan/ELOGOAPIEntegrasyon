using EBelgeAPI.Data.Interfaces;
using EBelgeAPI.Models.DTOs;
using EBelgeAPI.Models.Entities;
using EBelgeAPI.Services.Interfaces;
using Newtonsoft.Json;

namespace EBelgeAPI.Services;

public class LogoTokenService(
    ILogoTokenRepository tokenRepo,
    ILogoSettingsRepository settingsRepo,
    IHttpClientFactory httpClientFactory,
    ILogService log) : ILogoTokenService
{
    public async Task<LogoTokenDto?> GetOrFetchTokenAsync()
    {
        LogoTokenCache? cached = await tokenRepo.GetValidTokenAsync();
        if (cached != null)
            return ToDto(cached);
        LogoSettings? settings = await settingsRepo.GetActiveSettingsAsync();
        if (settings == null)
        {
            await log.ErrorAsync("Logo ayarları bulunamadı. LogoSettings tablosunu kontrol edin.", "System");
            return null;
        }
        LogoTokenCache? token = await FetchFromLogoAsync(settings);
        if (token == null) return null;
        await tokenRepo.SaveTokenAsync(token);
        return ToDto(token);
    }
    private async Task<LogoTokenCache?> FetchFromLogoAsync(LogoSettings settings)
    {
        try
        {
            HttpClient client = httpClientFactory.CreateClient("Logo");
            string? url = "https://idm.logo.cloud/legacy/sts/api/oauth/token";
            Dictionary<string, string> form = new Dictionary<string, string>
            {
                ["grant_type"] = "password",
                ["client_id"] = settings.ClientId,
                ["client_secret"] = settings.ClientSecret,
                ["customer_id"] = settings.CustomerId,
                ["username"] = settings.LogoUsername,
                ["password"] = settings.LogoPassword,
                ["firm"] = settings.FirmNr.Trim()
            };
            HttpResponseMessage resp = await client.PostAsync(url, new FormUrlEncodedContent(form));
            string? body = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
            {
                await log.ErrorAsync(
                    $"Logo token alınamadı. HTTP {(int)resp.StatusCode}.",
                    detail: body, source: "System");
                return null;
            }
            dynamic? data = JsonConvert.DeserializeObject(body);
            if (data == null)
            {
                await log.ErrorAsync("Logo token yanıtı parse edilemedi.", "System");
                return null;
            }
            string accessToken = data.access_token;
            int expiresIn = (int)(data.expires_in ?? 3600);
            return new LogoTokenCache
            {
                AccessToken = accessToken,
                ExpireDate = DateTime.UtcNow.AddSeconds(expiresIn),
                Server = settings.MachineId,
                Firm = settings.FirmNr.Trim(),
                CreatedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            await log.ErrorAsync(
                "Logo token alma sırasında beklenmedik hata oluştu.",
                detail: ex.ToString(), source: "System");
            return null;
        }
    }
    private static LogoTokenDto ToDto(LogoTokenCache c) => new()
    {
        AccessToken = c.AccessToken,
        ExpireDate = c.ExpireDate,
        Server = c.Server,
        Firm = c.Firm,
        RemainingMinutes = Math.Max(0, (int)(c.ExpireDate.ToUniversalTime() - DateTime.UtcNow).TotalMinutes)
    };
}