using System.Text;
using System.Text.Json;
using EBelgeAPI.Data.Interfaces;
using EBelgeAPI.Models.DTOs;
using EBelgeAPI.Models.Entities;
using EBelgeAPI.Services.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace EBelgeAPI.Services;

public class LotTakipService(
    HttpClient http,
    IServiceScopeFactory scopeFactory,
    IMemoryCache cache,
    ILogger<LotTakipService> logger) : ILotTakipService
{
    private const string CacheKey = "lot-takip-raporu";
    private const int PageSize = 1000;

    private readonly JsonSerializerOptions _jsonOpt = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
    private const string SqlTemplate =
         "SELECT SLN.CODE AS SERI_NO, ITM.CODE AS MALZEME_KODU, ITM.DESCRIPTION AS MALZEME_ADI, " +

         // ── GİRİŞ ─────────────────────────────────────────────────────────────
         "MAX(CASE WHEN SLTRANS.IOCATEGORY = 1 THEN CAST(SLIP.SLIPDATE AS DATE) END) AS GIRIS_TARIHI, " +
         "MAX(CASE WHEN SLTRANS.IOCATEGORY = 1 THEN CASE SLIP.SLIPTYPE WHEN 1 THEN 'Satinalma Faturasi' WHEN 5 THEN 'Konsiye Giris' ELSE CAST(SLIP.SLIPTYPE AS VARCHAR) END END) AS GIRIS_FIS_TIPI, " +
         "MAX(CASE WHEN SLTRANS.IOCATEGORY = 1 THEN COALESCE(INV.SLIPNR, SLIP.SLIPNR) END) AS GIRIS_FIS_NO, " +
         "MAX(CASE WHEN SLTRANS.IOCATEGORY = 1 THEN ARP.CODE END) AS GIRIS_CARI_KODU, " +
         "MAX(CASE WHEN SLTRANS.IOCATEGORY = 1 THEN ARP.DESCRIPTION END) AS GIRIS_CARI_UNVANI, " +
         "MAX(CASE WHEN SLTRANS.IOCATEGORY = 1 THEN SLTRANS.MAINQUANTITY END) AS GIRIS_MIKTAR, " +
         "MAX(CASE WHEN SLTRANS.IOCATEGORY = 1 THEN CAST(MMTRN.PRICE AS DECIMAL(18,2)) END) AS GIRIS_BIRIM_FIYAT, " +
         "MAX(CASE WHEN SLTRANS.IOCATEGORY = 1 THEN CAST(MMTRN.LINENET / NULLIF(MMTRN.QUANTITY, 0) AS DECIMAL(18,2)) END) AS GIRIS_KDV_MATRAHI, " +
         "MAX(CASE WHEN SLTRANS.IOCATEGORY = 1 THEN CAST(MMTRN.VATRATE AS DECIMAL(18,2)) END) AS GIRIS_KDV_ORANI, " +
         "MAX(CASE WHEN SLTRANS.IOCATEGORY = 1 THEN CAST(MMTRN.VATAMNT / NULLIF(MMTRN.QUANTITY, 0) AS DECIMAL(18,2)) END) AS GIRIS_KDV_TUTARI, " +
         "MAX(CASE WHEN SLTRANS.IOCATEGORY = 1 THEN CAST((MMTRN.LINENET + MMTRN.VATAMNT) / NULLIF(MMTRN.QUANTITY, 0) AS DECIMAL(18,2)) END) AS GIRIS_TOPLAM_TUTAR, " +
         // Giriş ambarı
         "MAX(CASE WHEN SLTRANS.IOCATEGORY = 1 THEN (SELECT ORG.DESCRIPTION FROM S_ORGUNITS ORG WHERE ORG.LOGICALREF = SLIP.WHREF) END) AS GIRIS_AMBAR, " +

         // ── ÇIKIŞ ─────────────────────────────────────────────────────────────
         "STRING_AGG(CASE WHEN SLTRANS.IOCATEGORY = 4 THEN CAST(CAST(SLIP.SLIPDATE AS DATE) AS VARCHAR) END, ' | ') AS CIKIS_TARIHLERI, " +
         "STRING_AGG(CASE WHEN SLTRANS.IOCATEGORY = 4 THEN CASE SLIP.SLIPTYPE WHEN 7 THEN 'Perakende Satis Irsaliyesi' WHEN 8 THEN 'Toptan Satis Irsaliyesi' WHEN 9 THEN 'Konsiye Cikis' ELSE CAST(SLIP.SLIPTYPE AS VARCHAR) END END, ' | ') AS CIKIS_FIS_TIPLERI, " +
         "STRING_AGG(CASE WHEN SLTRANS.IOCATEGORY = 4 THEN COALESCE(INV.SLIPNR, SLIP.SLIPNR) END, ' | ') AS CIKIS_FIS_NOLARI, " +
         "STRING_AGG(CASE WHEN SLTRANS.IOCATEGORY = 4 THEN ARP.CODE END, ' | ') AS CIKIS_CARI_KODLARI, " +
         "STRING_AGG(CASE WHEN SLTRANS.IOCATEGORY = 4 THEN ARP.DESCRIPTION END, ' | ') AS CIKIS_CARI_UNVANLARI, " +
         "STRING_AGG(CASE WHEN SLTRANS.IOCATEGORY = 4 THEN CAST(SLTRANS.MAINQUANTITY AS VARCHAR) END, ' | ') AS CIKIS_MIKTARLARI, " +
         "STRING_AGG(CASE WHEN SLTRANS.IOCATEGORY = 4 THEN CAST(CAST(MMTRN.PRICE AS DECIMAL(18,2)) AS VARCHAR) END, ' | ') AS CIKIS_BIRIM_FIYATLARI, " +
         "STRING_AGG(CASE WHEN SLTRANS.IOCATEGORY = 4 THEN CAST(CAST(MMTRN.LINENET / NULLIF(MMTRN.QUANTITY, 0) AS DECIMAL(18,2)) AS VARCHAR) END, ' | ') AS CIKIS_KDV_MATRAHLARI, " +
         "STRING_AGG(CASE WHEN SLTRANS.IOCATEGORY = 4 THEN CAST(CAST(MMTRN.VATRATE AS DECIMAL(18,2)) AS VARCHAR) END, ' | ') AS CIKIS_KDV_ORANLARI, " +
         "STRING_AGG(CASE WHEN SLTRANS.IOCATEGORY = 4 THEN CAST(CAST(MMTRN.VATAMNT / NULLIF(MMTRN.QUANTITY, 0) AS DECIMAL(18,2)) AS VARCHAR) END, ' | ') AS CIKIS_KDV_TUTARLARI, " +
         "STRING_AGG(CASE WHEN SLTRANS.IOCATEGORY = 4 THEN CAST(CAST((MMTRN.LINENET + MMTRN.VATAMNT) / NULLIF(MMTRN.QUANTITY, 0) AS DECIMAL(18,2)) AS VARCHAR) END, ' | ') AS CIKIS_TOPLAM_TUTARLARI, " +
         // Çıkış ambarı
         "STRING_AGG(CASE WHEN SLTRANS.IOCATEGORY = 4 THEN (SELECT ORG.DESCRIPTION FROM S_ORGUNITS ORG WHERE ORG.LOGICALREF = SLIP.WHREF) END, ' | ') AS CIKIS_AMBAR, " +
         // Satış elemanı
         "STRING_AGG(CASE WHEN SLTRANS.IOCATEGORY = 4 THEN (SELECT SP.DESCRIPTION FROM U_$V(firm)_SALESPERSONS SP WHERE SP.LOGICALREF = INV.SALEPERSONREF) END, ' | ') AS CIKIS_SATIS_ELEMANI, " +

         // ── ÖZET ──────────────────────────────────────────────────────────────
         "SUM(CASE WHEN SLTRANS.IOCATEGORY = 1 THEN SLTRANS.MAINQUANTITY ELSE 0 END) AS TOPLAM_GIRIS, " +
         "SUM(CASE WHEN SLTRANS.IOCATEGORY = 4 THEN SLTRANS.MAINQUANTITY ELSE 0 END) AS TOPLAM_CIKIS, " +
         "SUM(CASE WHEN SLTRANS.IOCATEGORY = 1 THEN SLTRANS.MAINQUANTITY ELSE 0 END) - SUM(CASE WHEN SLTRANS.IOCATEGORY = 4 THEN SLTRANS.MAINQUANTITY ELSE 0 END) AS KALAN_STOK, " +
         "CAST(SUM(CASE WHEN SLTRANS.IOCATEGORY = 1 THEN (MMTRN.LINENET + MMTRN.VATAMNT) / NULLIF(MMTRN.QUANTITY, 0) ELSE 0 END) AS DECIMAL(18,2)) AS TOPLAM_GIRIS_TUTAR, " +
         "CAST(SUM(CASE WHEN SLTRANS.IOCATEGORY = 4 THEN (MMTRN.LINENET + MMTRN.VATAMNT) / NULLIF(MMTRN.QUANTITY, 0) ELSE 0 END) AS DECIMAL(18,2)) AS TOPLAM_CIKIS_TUTAR, " +
         "CAST(SUM(CASE WHEN SLTRANS.IOCATEGORY = 4 THEN (MMTRN.LINENET + MMTRN.VATAMNT) / NULLIF(MMTRN.QUANTITY, 0) ELSE 0 END) - SUM(CASE WHEN SLTRANS.IOCATEGORY = 1 THEN (MMTRN.LINENET + MMTRN.VATAMNT) / NULLIF(MMTRN.QUANTITY, 0) ELSE 0 END) AS DECIMAL(18,2)) AS TUTAR_FARKI " +

         "FROM U_$V(firm)_SLNUMBERS SLN " +
         "LEFT JOIN U_$V(firm)_ITEMS ITM ON ITM.LOGICALREF = SLN.ITEMREF " +
         "LEFT JOIN U_$V(firm)_01_SLTRANS SLTRANS ON SLTRANS.SLREF = SLN.LOGICALREF " +
         "LEFT JOIN U_$V(firm)_01_MMTRANS MMTRN ON MMTRN.LOGICALREF = SLTRANS.MMSLIPLNREF " +
         "LEFT JOIN U_$V(firm)_01_MMSLIPS SLIP ON SLIP.LOGICALREF = MMTRN.MMSLIPREF " +
         "LEFT JOIN U_$V(firm)_ARPS ARP ON ARP.LOGICALREF = MMTRN.ARPREF " +
         "LEFT JOIN U_$V(firm)_01_INVOICES INV ON INV.LOGICALREF = SLIP.INVOICEREF " +
         "WHERE SLN.SLTYPE = 2 " +
         "GROUP BY SLN.CODE, ITM.CODE, ITM.DESCRIPTION " +
         "HAVING SUM(CASE WHEN SLTRANS.IOCATEGORY = 1 THEN SLTRANS.MAINQUANTITY ELSE 0 END) > 0 " +
         "OR SUM(CASE WHEN SLTRANS.IOCATEGORY = 4 THEN SLTRANS.MAINQUANTITY ELSE 0 END) > 0 " +
         "ORDER BY ITM.CODE, SLN.CODE " +
         "OFFSET {0} ROWS FETCH NEXT {1} ROWS ONLY";
    public async Task<(bool Success, List<LotTakipDto>? Data, string? Error)> GetLotTakipRaporuAsync()
    {
        // ── Cache kontrolü ────────────────────────────────
        if (cache.TryGetValue(CacheKey, out List<LotTakipDto>? cached))
        {
            logger.LogInformation("Lot takip raporu cacheden döndürüldü. {Count} kayıt.", cached!.Count);
            return (true, cached, null);
        }
        try
        {
            // ── Scoped servisleri çöz ─────────────────────
            using var scope = scopeFactory.CreateScope();
            ILogoSettingsRepository? logoSettingsRepo = scope.ServiceProvider.GetRequiredService<ILogoSettingsRepository>();
            ILogoTokenService? tokenService = scope.ServiceProvider.GetRequiredService<ILogoTokenService>();
            LogoSettings? logoSettings = await logoSettingsRepo.GetActiveSettingsAsync();
            if (logoSettings == null)
                return (false, null, "Logo ERP ayarları bulunamadı.");
            LogoTokenDto? token = await tokenService.GetOrFetchTokenAsync();
            if (token == null)
                return (false, null, "Logo ERP token alınamadı.");
            string baseUrl = logoSettings.ServerUrl.TrimEnd('/') + "/ERP-23/logo/restservices/rest";
            string firm = logoSettings.FirmNr.ToString();
            string queryUrl = $"{baseUrl}/dataQuery/executeSelectQuery";
            var tumData = new List<LotTakipDto>();
            int page = 0;
            // ── Sayfalı veri çekme ────────────────────────
            while (true)
            {
                int offset = page * PageSize;
                string sql = string.Format(SqlTemplate, offset, PageSize);
                var body = new
                {
                    querySqlText = sql,
                    dataQueryParams = $"{{\"firm\":\"{firm}\"}}",
                    jsonFormat = 1,
                    maxCount = PageSize
                };
                HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Post, queryUrl);
                req.Headers.Add("access-token", token.AccessToken);
                req.Headers.Add("firm", firm);
                req.Headers.Add("lang", "TRTR");
                req.Content = new StringContent(
                    JsonSerializer.Serialize(body, _jsonOpt),
                    Encoding.UTF8, "application/json");
                HttpResponseMessage resp = await http.SendAsync(req);
                string? respBody = await resp.Content.ReadAsStringAsync();
                if (!resp.IsSuccessStatusCode)
                    return (false, null, $"Logo API hata: {respBody}");
                JsonElement result = JsonSerializer.Deserialize<JsonElement>(respBody);
                if (!result.TryGetProperty("successful", out var success) || !success.GetBoolean())
                {
                    string? errMsg = result.TryGetProperty("errorMessage", out var em)
                        ? em.GetString() : "Bilinmeyen hata";
                    return (false, null, $"Logo sorgu hatası: {errMsg}");
                }
                if (!result.TryGetProperty("rows", out var rows) ||
                    rows.ValueKind == JsonValueKind.Null)
                    break;
                var rowList = rows.EnumerateArray().ToList();
                if (rowList.Count == 0) break;
                foreach (JsonElement row in rowList)
                    tumData.Add(MapRow(row));
                logger.LogInformation(
                    "Lot takip sayfa {Page}: {Count} kayıt çekildi. Toplam: {Total}",
                    page, rowList.Count, tumData.Count);
                if (rowList.Count < PageSize) break;
                page++;
            }
            // ── 30 dk cache'e yaz ─────────────────────────
            cache.Set(CacheKey, tumData, TimeSpan.FromMinutes(30));
            logger.LogInformation(
                "Lot takip raporu cache'e yazıldı. Toplam {Count} kayıt.", tumData.Count);
            return (true, tumData, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Lot takip raporu hatası.");
            return (false, null, ex.Message);
        }
    }
    public Task CacheSifirlaAsync()
    {
        cache.Remove(CacheKey);
        logger.LogInformation("Lot takip raporu cache sıfırlandı.");
        return Task.CompletedTask;
    }
    // ── Row → DTO ─────────────────────────────────────────
    private static LotTakipDto MapRow(JsonElement row)
    {
        static string? S(JsonElement r, string k) =>
            r.TryGetProperty(k, out var v) && v.ValueKind != JsonValueKind.Null
                ? v.GetString() : null;
        static decimal? D(JsonElement r, string k) =>
            r.TryGetProperty(k, out var v) && v.ValueKind != JsonValueKind.Null &&
            v.TryGetDecimal(out var d) ? d : null;
        static decimal Dz(JsonElement r, string k) => D(r, k) ?? 0;
        return new LotTakipDto
        {
            SeriNo = S(row, "SERI_NO"),
            MalzemeKodu = S(row, "MALZEME_KODU"),
            MalzemeAdi = S(row, "MALZEME_ADI"),
            GirisTarihi = DateTime.TryParse(S(row, "GIRIS_TARIHI"), out var dt) ? dt : null,
            GirisFisTipi = S(row, "GIRIS_FIS_TIPI"),
            GirisFisNo = S(row, "GIRIS_FIS_NO"),
            GirisCariKodu = S(row, "GIRIS_CARI_KODU"),
            GirisCariUnvani = S(row, "GIRIS_CARI_UNVANI"),
            GirisMiktar = D(row, "GIRIS_MIKTAR"),        // ← ekle
            GirisBirimFiyat = D(row, "GIRIS_BIRIM_FIYAT"),
            GirisKdvMatrahi = D(row, "GIRIS_KDV_MATRAHI"),
            GirisKdvOrani = D(row, "GIRIS_KDV_ORANI"),
            GirisKdvTutari = D(row, "GIRIS_KDV_TUTARI"),
            GirisToplamTutar = D(row, "GIRIS_TOPLAM_TUTAR"),
            CikisTarihleri = S(row, "CIKIS_TARIHLERI"),
            CikisFisTipleri = S(row, "CIKIS_FIS_TIPLERI"),
            CikisFisNolari = S(row, "CIKIS_FIS_NOLARI"),
            CikisCariKodlari = S(row, "CIKIS_CARI_KODLARI"),
            CikisCariUnvanlari = S(row, "CIKIS_CARI_UNVANLARI"),
            CikisMiktarlari = S(row, "CIKIS_MIKTARLARI"),    // ← ekle
            CikisBirimFiyatlari = S(row, "CIKIS_BIRIM_FIYATLARI"),
            CikisKdvMatrahlari = S(row, "CIKIS_KDV_MATRAHLARI"),
            CikisKdvOranlari = S(row, "CIKIS_KDV_ORANLARI"),
            CikisKdvTutarlari = S(row, "CIKIS_KDV_TUTARLARI"),
            CikisToplamTutarlari = S(row, "CIKIS_TOPLAM_TUTARLARI"),
            ToplamGiris = Dz(row, "TOPLAM_GIRIS"),
            ToplamCikis = Dz(row, "TOPLAM_CIKIS"),
            KalanStok = Dz(row, "KALAN_STOK"),
            ToplamGirisTutar = Dz(row, "TOPLAM_GIRIS_TUTAR"),
            ToplamCikisTutar = Dz(row, "TOPLAM_CIKIS_TUTAR"),
            TutarFarki = Dz(row, "TUTAR_FARKI"),
            GirisAmbar = S(row, "GIRIS_AMBAR"),
            CikisAmbar = S(row, "CIKIS_AMBAR"),
            CikisSatisElemani = S(row, "CIKIS_SATIS_ELEMANI"),
        };
    }
}