using EBelgeAPI.Data.Interfaces;
using EBelgeAPI.Models.DTOs;
using EBelgeAPI.Models.Entities;
using EBelgeAPI.Services.Interfaces;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Web;
using static System.Net.WebRequestMethods;

namespace EBelgeAPI.Services;
public class LogoTransferService : ILogoTransferService
{
    private readonly HttpClient _http;
    private readonly ILogoTokenService _tokenService;
    private readonly ITransferRepository _transferRepo;
    private readonly ILogoSettingsRepository _logoSettingsRepo;
    private readonly ILogoItemCacheService _itemCache;       // ← eklendi
    private readonly ILogger<LogoTransferService> _logger;
    private readonly ILogService _logService;
 
   

    private readonly JsonSerializerOptions _jsonOpt = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
    public LogoTransferService(
     HttpClient http,
     IHttpClientFactory httpClientFactory,
     ILogoTokenService tokenService,
     ITransferRepository transferRepo,
     ILogoSettingsRepository logoSettingsRepo,
     ILogoItemCacheService itemCache,
     ILogger<LogoTransferService> logger)
    {
        _http = http;
        _tokenService = tokenService;
        _transferRepo = transferRepo;
        _logoSettingsRepo = logoSettingsRepo;
        _itemCache = itemCache;
        _logger = logger;

    }   

    // ── TOPLU TRANSFER ────────────────────────────────────
    public async Task<List<SalesTransferResultDto>> TopluTransferAsync(
        List<SalesInvoiceDto> dtos, string ambarKodu, string satisElemaniKodu)
    {
        SemaphoreSlim semaphore = new SemaphoreSlim(3);
        var tasks = dtos.Select(async dto =>
        {
            await semaphore.WaitAsync();
            try
            {
                var (ok, result, err) = await TransferAsync(dto, ambarKodu, satisElemaniKodu);
                return result ?? new SalesTransferResultDto
                {
                    Uuid = dto.Uuid,
                    ELogoFaturaNo = dto.FaturaNo,
                    Success = false,
                    Error = err
                };
            }
            finally { semaphore.Release(); }
        });
        return (await Task.WhenAll(tasks)).ToList();
    }
    // ── TEK TRANSFER ──────────────────────────────────────
    public async Task<(bool Success, SalesTransferResultDto? Result, string? Error)>
        TransferAsync(SalesInvoiceDto dto, string ambarKodu, string satisElemaniKodu)
    {
        // Daha önce başarıyla aktarıldı mı?
        ELogoLogoTransfer? mevcutBasarili = await _transferRepo.GetBasariliByUuidAsync(dto.Uuid!);
        if (mevcutBasarili != null)
            return (false, null, "Bu fatura daha önce aktarıldı.");
        ELogoLogoTransfer? transfer = await _transferRepo.GetByUuidAsync(dto.Uuid!)
                       ?? new ELogoLogoTransfer
                       {
                           ELogoUuid = dto.Uuid!,
                           ELogoFaturaNo = dto.FaturaNo,
                           CreatedAt = DateTime.Now
                       };
        transfer.AktarimDurumu = 0;
        transfer.HataMesaji = null;
        transfer.LogoFaturaNo = null;
        transfer.LogoLogicalRef = null;
        transfer.AktarimTarihi = null;
        if (transfer.Id == 0)
            transfer.Id = await _transferRepo.CreateAsync(transfer);
        else
            await _transferRepo.UpdateAsync(transfer);
        try
        {
            LogoTokenDto? token = await _tokenService.GetOrFetchTokenAsync();
            if (token == null)
                throw new Exception("Logo ERP token alınamadı.");
            LogoSettings? logoSettings = await _logoSettingsRepo.GetActiveSettingsAsync();
            if (logoSettings == null)
                throw new Exception("Logo ERP ayarları bulunamadı.");
            string baseUrl = logoSettings.ServerUrl.TrimEnd('/') + "/ERP-23/logo/restservices/rest";
            string firm = logoSettings.FirmNr.ToString();
            // ── Cari bul/oluştur ──
            string? arpCode = await FindOrCreateArpCodeAsync(dto, token.AccessToken, baseUrl, firm);
            if (string.IsNullOrEmpty(arpCode))
                throw new Exception($"Cari bulunamadı/oluşturulamadı. VKN: {dto.AliciVkn} TCKN: {dto.AliciTckn}");
            // ── Malzeme cardType'larını çek (cache'ten) ──
            var kalemCardTypes = await ResolveItemCardTypesAsync(dto.Kalemler);
            // ── Logo JSON oluştur ──
            var logoJson = await BuildLogoInvoiceJsonAsync(
                dto, arpCode, ambarKodu, satisElemaniKodu, kalemCardTypes);
            string? logoJsonStr = JsonSerializer.Serialize(logoJson, _jsonOpt);
            _logger.LogInformation("Logo transfer JSON: {Json}", logoJsonStr);
            // ── Logo ERP'ye gönder ──
            HttpRequestMessage request = new HttpRequestMessage(
                HttpMethod.Post,
                $"{baseUrl}/v2.0/invoices/sales?invoiceType=7");
            request.Headers.Add("access-token", token.AccessToken);
            request.Headers.Add("firm", firm);
            request.Headers.Add("lang", "TRTR");
            request.Content = new StringContent(logoJsonStr, Encoding.UTF8, "application/json");
            HttpResponseMessage resp = await _http.SendAsync(request);
            string? body = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
                throw new Exception($"Logo ERP hata: {body}");
            var result = JsonSerializer.Deserialize<JsonElement>(body, _jsonOpt);
            string? logoFaturaNo = result.GetProperty("code").GetString();
            int logoRef = result.GetProperty("logicalRef").GetInt32();
            transfer.AktarimDurumu = 1;
            transfer.LogoFaturaNo = logoFaturaNo;
            transfer.LogoLogicalRef = logoRef;
            transfer.AktarimTarihi = DateTime.Now;
            await _transferRepo.UpdateAsync(transfer);

            return (true, new SalesTransferResultDto
            {
                Uuid = dto.Uuid,
                ELogoFaturaNo = dto.FaturaNo,
                Success = true,
                LogoFaturaNo = logoFaturaNo,
                LogoLogicalRef = logoRef
            }, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Logo transfer hatası. UUID: {Uuid}", dto.Uuid);
            transfer.AktarimDurumu = 2;
            transfer.HataMesaji = ex.Message;
            transfer.AktarimTarihi = DateTime.Now;
            await _transferRepo.UpdateAsync(transfer);
            return (false, null, ex.Message);
        }
    }
    // ── Malzeme cardType'larını toplu çöz ─────────────────
    private async Task<Dictionary<string, int>> ResolveItemCardTypesAsync(
        List<SalesInvoiceLineDto> kalemler)
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var kodlar = kalemler
            .Select(k => k.LogoMalzemeKodu)
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        foreach (string kod in kodlar)
        {
            var cardType = await _itemCache.GetCardTypeAsync(kod);
            if (cardType.HasValue)
                result[kod] = cardType.Value;
            else
                _logger.LogWarning("Malzeme bulunamadı cache'te: {Kod}", kod);
        }
        return result;
    }
    private Task<object> BuildLogoInvoiceJsonAsync(
            SalesInvoiceDto dto,
            string arpCode,
            string ambarKodu,
            string satisElemaniKodu,
            Dictionary<string, int> kalemCardTypes)
    {
        string tarih = dto.FaturaTarihi?.ToString("yyyy-MM-ddTHH:mm:ss.fffzzz")
                    ?? DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fffzzz");
        List<object> kalemler = new List<object>();
        string? notlar = dto.FaturaNotu;
        foreach (SalesInvoiceLineDto k in dto.Kalemler)
        {
            string malzemeKodu = k.LogoMalzemeKodu;
            kalemCardTypes.TryGetValue(malzemeKodu, out int cardType);
            int logoType = cardType == 30 ? 4 : 0;
            // SeriNo varsa vatincluded=false (telefon/elektronik), yoksa true
            bool vatIncluded = string.IsNullOrEmpty(k.SeriNo);
            // UBL'de:
            // SatirToplam = LineExtensionAmount (iskonto ONCESI, ornek: 3980)
            // IskontoTutar = AllowanceCharge.Amount (ornek: 210)
            // KdvTutar = TaxAmount (iskonto SONRASI matrah uzerinden, ornek: 796 = 3770 * 20%)
            // BirimFiyat = PriceAmount (KDV dahil liste fiyati, ornek: 4190)

            // vatincluded=false (telefon):
            //   unitPrice = BirimFiyat (PriceAmount = 4190)
            //   Logo iskonto duser: 4190 - 210 = 3980 matrah
            //   KDV: 3980 * 20% = 796
            //   Toplam: 4776
            //
            // vatincluded=true (gida):
            //   unitPrice = (SatirToplam + KdvTutar) / Miktar
            //   KDV dahil birim fiyat
            decimal unitPrice = vatIncluded
                ? (k.Miktar > 0 ? (k.SatirToplam + k.KdvTutar) / k.Miktar : k.BirimFiyat)
                : k.BirimFiyat;  // PriceAmount direkt kullan
            // vatbase: iskonto sonrasi matrah
            // vatincluded=false: SatirToplam - IskontoTutar = 3980 - 210 = 3770
            // vatincluded=true:  SatirToplam (zaten net)
            decimal vatBase = vatIncluded
                ? k.SatirToplam
                : k.SatirToplam - k.IskontoTutar;
            if (vatBase < 0) vatBase = k.SatirToplam;
            bool iskontoVar = k.IskontoTutar > 0;
            _logger.LogInformation(
                "Kalem parse: Kod={Kod} BirimFiyat={Fiyat} SatirToplam={Toplam} " +
                "Iskonto={Iskonto} KdvTutar={Kdv} SeriNo={Seri} " +
                "=> unitPrice={UP} vatBase={VB} vatIncluded={VI}",
                malzemeKodu, k.BirimFiyat, k.SatirToplam,
                k.IskontoTutar, k.KdvTutar, k.SeriNo,
                unitPrice, vatBase, vatIncluded);
            Dictionary<string, object?> kalem = new Dictionary<string, object?>
            {
                ["deep"] = false,
                ["type"] = logoType,
                ["code"] = malzemeKodu,
                ["description"] = k.Aciklama ?? k.UrunAdi ?? malzemeKodu,
                ["quantity"] = k.Miktar,
                ["unit"] = 46,
                ["unitCode"] = MapUnitCode(k.BirimKodu),
                ["unitPrice"] = unitPrice,
                ["currencyTypeRC"] = 1,
                ["netDiscount"] = false,
                ["vatratePercent"] = k.KdvOrani,
                ["vatincluded"] = vatIncluded,
                ["vatamount"] = k.KdvTutar,
                ["vatbase"] = vatBase,
                ["gstincluded"] = false,
                ["amount"] = vatIncluded
    ? k.SatirToplam + k.KdvTutar
    : k.BirimFiyat * k.Miktar,  // 4190 × 1 = 4190
                ["netAmount"] = vatIncluded
    ? k.SatirToplam
    : (k.BirimFiyat * k.Miktar) - k.IskontoTutar,  // 4190 - 210 = 3980
                ["costType"] = -1,
                ["distributionType"] = -1,
                ["foreignTradeType"] = -1,
                ["warehouse"] = ambarKodu,
                ["purchaseEmployeeSalespersonCode"] = satisElemaniKodu,
                ["slDetailsTransaction"] = string.IsNullOrEmpty(k.SeriNo)
                    ? Array.Empty<object>()
                    : new object[]
                    {
                        new
                        {
                            serialNumber             = k.SeriNo.Trim(),  
                   
                            quantity              = k.Miktar,
                   
                        }
                    },
                ["medDeviceDetailTransaction"] = Array.Empty<object>()
            };
            kalemler.Add(kalem);
            // Iskonto satiri
            if (iskontoVar)
            {
                kalemler.Add(new Dictionary<string, object?>
                {
                    ["deep"] = true,
                    ["type"] = 2,
                    ["currencyTypeRC"] = 1,
                    ["amount"] = k.IskontoTutar,
                    ["netDiscount"] = false,
                    ["vatincluded"] = false,
                    ["gstincluded"] = false,
                    ["costType"] = -1,
                    ["distributionType"] = -1,
                    ["foreignTradeType"] = -1,
                    ["warehouse"] = ambarKodu,
                    ["purchaseEmployeeSalespersonCode"] = satisElemaniKodu
                });
            }
        }
        bool isVkn = !string.IsNullOrEmpty(dto.AliciVkn);
        string kimlik = isVkn ? dto.AliciVkn! : dto.AliciTckn!;
        string unvan = dto.AliciUnvan ?? kimlik;
        var json = (object)new
        {
            no = dto.FaturaNo,
            date = tarih,
            documentDate = tarih,
            orgUnit = "01",
            orgunit2 = "01",
            warehouse = ambarKodu,
            warehouse2 = ambarKodu,
            department = "01",
            arap = arpCode,
            footnote = !string.IsNullOrWhiteSpace(dto.FaturaNotu) ? dto.FaturaNotu : null,
            araptitle = unvan,
            araptitle2 = unvan,
            araptitle3 = unvan,
            title = unvan,
            auxCode="AKTARIM",
            customer = kimlik,
            customer2 = kimlik,
            trIdentificationNo = kimlik,
            codeShipTo = kimlik,
            salespersonCode = satisElemaniKodu,
            eInvoice = false,
            eInvoice2 = false,
            eArchive = false,
            eArchive2 = dto.DocType == "earchive",
            electronicDocument = true,
            electronicDocument2 = false,
            electronicDocument3 = true,
            substitutesDispatchReceipt = true,
            sendingMethod = 1,
            legalEntity = isVkn,
            privateCompany = !isVkn,
            generalCurrency = 1,
            linesCurrency = 1,
            remainingRate = 100.0,
            distributeDiscounts = true,
            distributePromotions = true,
            distributeExpenses = true,
            distributeReverseCharge = true,
            reverseChargeRatePart1 = 2.0,
            reverseChargeRatePart2 = 3.0,
            deductionRatePart = 2.0,
            deductionRatePart2 = 3.0,
            reverseChargeApplicability = -1,
            applicablePercentofTaxRate = 100.0,
            masterDataDispatcDTO = new[]
    {
        new
        {
            type        = 7,
            date        = tarih,   // ← fatura tarihi
            documenDate = tarih    // ← fatura tarihi
        }
    },
            itemTransactionDTO = kalemler.ToArray()
        };
        return Task.FromResult(json);
    }

    // ── Cari bul / oluştur ────────────────────────────────
    private async Task<string?> FindOrCreateArpCodeAsync(
        SalesInvoiceDto dto, string token, string baseUrl, string firm)
    {
        bool isVkn = !string.IsNullOrEmpty(dto.AliciVkn);
        string kimlikNo = isVkn ? dto.AliciVkn! : dto.AliciTckn!;
        string where = isVkn
            ? $"TAXNR='{kimlikNo}'"
            : $"IDTCNO='{kimlikNo}'";
        var queryBody = new
        {
            querySqlText = $"SELECT CODE FROM U_$V(firm)_ARPS WHERE {where}",
            dataQueryParams = $"{{\"firm\":\"{firm}\"}}",
            jsonFormat = 1,
            maxCount = 1
        };
        HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Post,
            $"{baseUrl}/dataQuery/executeSelectQuery");
        req.Headers.Add("access-token", token);
        req.Headers.Add("firm", firm);
        req.Headers.Add("lang", "TRTR");
        req.Content = new StringContent(
            JsonSerializer.Serialize(queryBody), Encoding.UTF8, "application/json");
        HttpResponseMessage resp = await _http.SendAsync(req);
        string? json = await resp.Content.ReadAsStringAsync();
        _logger.LogInformation("FindArpCode response: {Json}", json);
        try
        {
            var result = JsonSerializer.Deserialize<JsonElement>(json);
            if (result.TryGetProperty("rows", out var rows))
            {
                var arr = rows.EnumerateArray().ToList();
                if (arr.Count > 0 && arr[0].TryGetProperty("CODE", out var code))
                    return code.GetString();
            }
        }
        catch { }
        _logger.LogInformation("Cari bulunamadı, oluşturuluyor. Kimlik: {Kimlik}", kimlikNo);
        return await CreateArpAsync(dto, token, baseUrl, firm);
    }
    private async Task<string?> CreateArpAsync(
        SalesInvoiceDto dto, string token, string baseUrl, string firm)
    {
        bool isVkn = !string.IsNullOrEmpty(dto.AliciVkn);
        string arpCode = isVkn ? dto.AliciVkn! : dto.AliciTckn!;
        string baslik = !string.IsNullOrWhiteSpace(dto.AliciUnvan)
            ? dto.AliciUnvan : arpCode;
        object arp;
        if (isVkn)
        {

            // Kurumsal:
            arp = new
            {
                code = arpCode,
                title = baslik,
                orgUnit = "01",
                cardtype = 3,
                taxNo = arpCode,
                privateCompany = false,
                foreignNational = false,
                phoneNo = !string.IsNullOrWhiteSpace(dto.AliciTelefon) ? dto.AliciTelefon : null,
                mobilePhone = !string.IsNullOrWhiteSpace(dto.AliciTelefon) ? dto.AliciTelefon : null,
                town2 = !string.IsNullOrWhiteSpace(dto.AliciIlce) ? dto.AliciIlce : null,
                city2 = !string.IsNullOrWhiteSpace(dto.AliciIl) ? dto.AliciIl : null,
                country = !string.IsNullOrWhiteSpace(dto.AliciUlke) ? dto.AliciUlke : "TR",
                salesManagement = true,
                financeManagement = true,
                purchaseManagement = false,
                potential = true,
                contractor = true
            };
        }
        else
        {
            // ── Şahıs ──
            // Ad soyad ayır: "VELAT ÖZÇELİK" → name=VELAT, surname=ÖZÇELİK
            string[] parcalar = baslik.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            string ad = parcalar.Length > 0 ? parcalar[0] : baslik;
            string soyad = parcalar.Length > 1 ? parcalar[1] : "-";
            // Şahıs:
            arp = new
            {
                code = arpCode,
                title = baslik,
                orgUnit = "01",
                cardtype = 3,
                tridentificationNo = arpCode,
                privateCompany = true,
                foreignNational = false,
                name = ad,
                surname = soyad,
                phoneNo = !string.IsNullOrWhiteSpace(dto.AliciTelefon) ? dto.AliciTelefon : null,
                mobilePhone = !string.IsNullOrWhiteSpace(dto.AliciTelefon) ? dto.AliciTelefon : null,
                town2 = !string.IsNullOrWhiteSpace(dto.AliciIlce) ? dto.AliciIlce : null,
                city2 = !string.IsNullOrWhiteSpace(dto.AliciIl) ? dto.AliciIl : null,
                country = !string.IsNullOrWhiteSpace(dto.AliciUlke) ? dto.AliciUlke : "TR",
                salesManagement = true,
                financeManagement = true,
                purchaseManagement = false,
                potential = true,
                contractor = true
            };

        }
        HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/v2.0/arps");
        req.Headers.Add("access-token", token);
        req.Headers.Add("firm", firm);
        req.Headers.Add("lang", "TRTR");
        req.Content = new StringContent(
            JsonSerializer.Serialize(arp, _jsonOpt),
            Encoding.UTF8, "application/json");
        HttpResponseMessage resp = await _http.SendAsync(req);
        string? body = await resp.Content.ReadAsStringAsync();
        _logger.LogInformation("CreateArp response: {Body}", body);
        if (!resp.IsSuccessStatusCode)
        {
            if (body.Contains("Aynı özelliklerde kayıt mevcut"))
            {
                _logger.LogInformation("Cari zaten var: {Code}", arpCode);
                return arpCode;
            }
            throw new Exception($"Cari oluşturulamadı: {body}");
        }
        return arpCode;
    }
    // ── Fatura Logo'da var mı? ────────────────────────────
    public async Task<bool> FaturaLogodaVarMiAsync(string faturaNo)
    {
        try
        {
            LogoTokenDto? token = await _tokenService.GetOrFetchTokenAsync();
            LogoSettings? logoSettings = await _logoSettingsRepo.GetActiveSettingsAsync();
            string baseUrl = logoSettings!.ServerUrl.TrimEnd('/') + "/ERP-23/logo/restservices/rest";
            HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Get,
                $"{baseUrl}/v2.0/invoices?invoiceNo={Uri.EscapeDataString(faturaNo)}&invoiceType=7&isOutgoing=true");
            req.Headers.Add("access-token", token!.AccessToken);
            req.Headers.Add("firm", logoSettings.FirmNr.ToString());
            req.Headers.Add("lang", "TRTR");
            HttpResponseMessage resp = await _http.SendAsync(req);
            if (!resp.IsSuccessStatusCode) return false;
            string? body = await resp.Content.ReadAsStringAsync();
            var json = JsonSerializer.Deserialize<JsonElement>(body);
            if (json.TryGetProperty("no", out var no) && !string.IsNullOrEmpty(no.GetString()))
                return true;
            return false;
        }
        catch { return false; }
    }
    // ── Helpers ───────────────────────────────────────────
    private static string MapUnitCode(string? unitCode) => unitCode switch
    {
        "NIU" => "ADET",
        "BX" => "KUTU",
        "KGM" => "KG",
        "LTR" => "LT",
        "MTR" => "MT",
        _ => "ADET"
    };
}