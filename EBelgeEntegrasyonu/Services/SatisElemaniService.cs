using System.Text.Json;
using EBelgeAPI.Data.Interfaces;
using EBelgeAPI.Models.DTOs;
using EBelgeAPI.Models.Entities;
using EBelgeAPI.Services.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace EBelgeAPI.Services;

public class SatisElemaniService : ISatisElemaniService
{
    private readonly HttpClient _http;
    private readonly ILogoTokenService _tokenService;
    private readonly ILogoSettingsRepository _logoSettingsRepo;
    private readonly IMemoryCache _cache;
    private readonly ILogger<SatisElemaniService> _logger;
    private const string CacheKey = "satis_elemanlari";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);
    private readonly JsonSerializerOptions _jsonOpt = new()
    {
        PropertyNameCaseInsensitive = true
    };
    public SatisElemaniService(
        HttpClient http,
        ILogoTokenService tokenService,
        ILogoSettingsRepository logoSettingsRepo,
        IMemoryCache cache,
        ILogger<SatisElemaniService> logger)
    {
        _http = http;
        _tokenService = tokenService;
        _logoSettingsRepo = logoSettingsRepo;
        _cache = cache;
        _logger = logger;
    }
    public async Task<List<SatisElemaniDto>> GetListAsync()
    {
        // Cache'te var mı?
        if (_cache.TryGetValue(CacheKey, out List<SatisElemaniDto>? cached) && cached != null)
        {
            _logger.LogInformation("Satış elemanları cache'ten alındı. Adet: {Count}", cached.Count);
            return cached;
        }
        // Logo'dan çek
        LogoTokenDto? token = await _tokenService.GetOrFetchTokenAsync();
        if (token == null)
            throw new Exception("Logo ERP token alınamadı.");
        LogoSettings? logoSettings = await _logoSettingsRepo.GetActiveSettingsAsync();
        if (logoSettings == null)
            throw new Exception("Logo ERP ayarları bulunamadı.");
        string baseUrl = logoSettings.ServerUrl.TrimEnd('/') + "/ERP-23/logo/restservices/rest";
        string firm = logoSettings.FirmNr.ToString();
        HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Get,
           $"{baseUrl}/v2.0/salespersonCard/list?limit=100");
        req.Headers.Add("access-token", token.AccessToken);
        req.Headers.Add("firm", firm);
  
        req.Headers.Add("lang", "TRTR");
        HttpResponseMessage resp = await _http.SendAsync(req);
        string? body = await resp.Content.ReadAsStringAsync();
        _logger.LogInformation("Satış elemanları Logo'dan alındı.");
        var result = JsonSerializer.Deserialize<JsonElement>(body, _jsonOpt);
        List<SatisElemaniDto> list = new List<SatisElemaniDto>();
        if (result.TryGetProperty("rows", out var rows))
        {
            foreach (var row in rows.EnumerateArray())
            {
                list.Add(new SatisElemaniDto
                {
                    Reference = row.TryGetProperty("Reference", out var r) ? r.GetInt32() : 0,
                    Code = row.TryGetProperty("Code", out var c) ? c.GetString() ?? "" : "",
                    Description = row.TryGetProperty("Description", out var d) ? d.GetString() ?? "" : ""
                });
            }
        }
        // Cache'e at
        _cache.Set(CacheKey, list, CacheDuration);
        _logger.LogInformation("Satış elemanları cache'e atıldı. Adet: {Count}", list.Count);
        return list;
    }
}