using System.Text;
using System.Text.Json;
using EBelgeAPI.Data.Interfaces;
using EBelgeAPI.Models.DTOs;
using EBelgeAPI.Models.Entities;
using EBelgeAPI.Services.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace EBelgeAPI.Services;
public class LogoItemCacheService : ILogoItemCacheService
{
    private readonly HttpClient _http;
    private readonly ILogoTokenService _tokenService;
    private readonly ILogoSettingsRepository _logoSettingsRepo;
    private readonly IMemoryCache _cache;
    private readonly ILogger<LogoItemCacheService> _logger;
    private const string CacheKey = "logo_item_cardtypes";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(1);
    private static readonly SemaphoreSlim _refreshLock = new(1, 1);
    public LogoItemCacheService(
        HttpClient http,
        ILogoTokenService tokenService,
        ILogoSettingsRepository logoSettingsRepo,
        IMemoryCache cache,
        ILogger<LogoItemCacheService> logger)
    {
        _http = http;
        _tokenService = tokenService;
        _logoSettingsRepo = logoSettingsRepo;
        _cache = cache;
        _logger = logger;
    }
    // ── Tek malzeme sorgula ──────────────────────────────
    public async Task<int?> GetCardTypeAsync(string itemCode)
    {
        var dict = await GetOrLoadCacheAsync();
        return dict.TryGetValue(itemCode.Trim(), out int cardType) ? cardType : null;
    }
    // ── Cache'i manuel yenile ────────────────────────────
    public async Task RefreshAsync()
    {
        _cache.Remove(CacheKey);
        await GetOrLoadCacheAsync();
        _logger.LogInformation("Logo malzeme cache yenilendi.");
    }
    // ── Cache yoksa yükle ────────────────────────────────
    private async Task<Dictionary<string, int>> GetOrLoadCacheAsync()
    {
        if (_cache.TryGetValue(CacheKey, out Dictionary<string, int>? cached) && cached != null)
            return cached;
        // Eş zamanlı yüklemeyi engelle
        await _refreshLock.WaitAsync();
        try
        {
            // Double-check
            if (_cache.TryGetValue(CacheKey, out cached) && cached != null)
                return cached;
            var dict = await LoadFromLogoAsync();
            _cache.Set(CacheKey, dict, CacheTtl);
            _logger.LogInformation(
                "Logo malzeme cache yüklendi. {Count} malzeme.", dict.Count);
            return dict;
        }
        finally
        {
            _refreshLock.Release();
        }
    }
    // ── Logo'dan tüm malzemeleri çek ─────────────────────
    private async Task<Dictionary<string, int>> LoadFromLogoAsync()
    {
        var dict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        try
        {
            LogoTokenDto? token = await _tokenService.GetOrFetchTokenAsync();
            if (token == null)
            {
                _logger.LogWarning("Logo token alınamadı, malzeme cache yüklenemedi.");
                return dict;
            }
            LogoSettings? logoSettings = await _logoSettingsRepo.GetActiveSettingsAsync();
            if (logoSettings == null)
            {
                _logger.LogWarning("Logo ayarları bulunamadı, malzeme cache yüklenemedi.");
                return dict;
            }
            string baseUrl = logoSettings.ServerUrl.TrimEnd('/') + "/ERP-23/logo/restservices/rest";
            string firm = logoSettings.FirmNr.ToString();
            var queryBody = new
            {
                querySqlText = "SELECT CODE, CARDTYPE FROM U_$V(firm)_ITEMS WHERE BOSTATUS = 0",
                dataQueryParams = $"{{\"firm\":\"{firm}\"}}",
                jsonFormat = 1
                // maxCount yok → tümü gelir
            };
            HttpRequestMessage req = new HttpRequestMessage(
                HttpMethod.Post,
                $"{baseUrl}/dataQuery/executeSelectQuery");
            req.Headers.Add("access-token", token.AccessToken);
            req.Headers.Add("firm", firm);
            req.Headers.Add("lang", "TRTR");
            req.Content = new StringContent(
                JsonSerializer.Serialize(queryBody),
                Encoding.UTF8,
                "application/json");
            HttpResponseMessage resp = await _http.SendAsync(req);
            string? json = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("Logo malzeme sorgusu başarısız: {Body}", json);
                return dict;
            }
            var result = JsonSerializer.Deserialize<JsonElement>(json);
            if (!result.TryGetProperty("rows", out var rows))
                return dict;
            foreach (var row in rows.EnumerateArray())
            {
                if (!row.TryGetProperty("CODE", out var codeEl)) continue;
                if (!row.TryGetProperty("CARDTYPE", out var typeEl)) continue;
                string? code = codeEl.GetString();
                if (string.IsNullOrEmpty(code)) continue;
                int cardType = typeEl.GetInt32();
                dict[code.Trim()] = cardType;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Logo malzeme cache yükleme hatası.");
        }
        return dict;
    }
}