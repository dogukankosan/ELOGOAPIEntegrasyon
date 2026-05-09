using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using EBelgeAPI.Data.Interfaces;
using EBelgeAPI.Models.ELogo;
using EBelgeAPI.Models.Entities;
using EBelgeAPI.Models.Requests;
using EBelgeAPI.Services.Interfaces;

namespace EBelgeAPI.Services;

// ── DOCUMENT TYPE ENUM ────────────────────────────────
public enum ELogoDocumentType
{
    EInvoice,
    EArchive,
    EWaybill
}

public class ELogoService : IELogoService
{
    private readonly HttpClient _http;
    private readonly IELogoSettingsRepository _settingsRepo;
    private readonly ILogger<ELogoService> _logger;
    private ELogoSettings? _settings;

    private async Task<ELogoSettings?> GetSettingsAsync()
    {
        _settings ??= await _settingsRepo.GetActiveAsync();
        return _settings;
    }
    public ELogoService(
        HttpClient http,
        IELogoSettingsRepository settingsRepo,
        ILogger<ELogoService> logger)
    {
        _http = http;
        _settingsRepo = settingsRepo;
        _logger = logger;
    }
    // ── HELPER: ENUM → STRING ─────────────────────────────
    private static string ToDocTypeString(ELogoDocumentType t) => t switch
    {
        ELogoDocumentType.EArchive => "EARCHIVE",
        ELogoDocumentType.EWaybill => "DESPATCHADVICE",
        _ => "EINVOICE"
    };
    // ── LOGIN ─────────────────────────────────────────────
    public async Task<(bool Success, string? Session, string? Error)> LoginAsync()
    {
        try
        {
            ELogoSettings? settings = await GetSettingsAsync();
            if (settings == null)
                return (false, null, "e-Logo ayarları bulunamadı.");
            string soap = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
  <soap:Body>
    <Login xmlns=""http://tempuri.org/"">
      <login xmlns:a=""http://schemas.datacontract.org/2004/07/eFaturaWebService"">
        <a:appStr>EBelgeAPI</a:appStr>
        <a:passWord>{settings.Password}</a:passWord>
        <a:source>0</a:source>
        <a:userName>{settings.Username}</a:userName>
        <a:version>1.0</a:version>
      </login>
    </Login>
  </soap:Body>
</soap:Envelope>";
            string raw = await SoapPostAsync(soap, "Login", settings.Url);
            string? session = XDocument.Parse(raw)
                .Descendants()
                .FirstOrDefault(e => e.Name.LocalName == "sessionID")?.Value;
            if (string.IsNullOrEmpty(session))
                return (false, null, "e-Logo session alınamadı.");
            return (true, session, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ELogo Login hatası");
            return (false, null, $"e-Logo bağlantı hatası: {ex.Message}");
        }
    }
    // ── LOGOUT ────────────────────────────────────────────
    public async Task LogoutAsync(string session)
    {
        try
        {
            ELogoSettings? settings = await GetSettingsAsync();
            if (settings == null) return;
            string soap = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
  <soap:Body>
    <Logout xmlns=""http://tempuri.org/"">
      <sessionID>{session}</sessionID>
    </Logout>
  </soap:Body>
</soap:Envelope>";
            await SoapPostAsync(soap, "Logout", settings.Url);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ELogo Logout hatası");
        }
    }

    // ── BELGE LİSTESİ ─────────────────────────────────────
    public async Task<(bool Success, List<ELogoInvoiceItem>? Items, string? Error)>
        GetInvoiceListByDateAsync(
            string session,
            DateTime begin,
            DateTime end,
            ELogoDocumentType documentType = ELogoDocumentType.EInvoice)
    {
        try
        {
            ELogoSettings? settings = await GetSettingsAsync();
            if (settings == null)
                return (false, null, "e-Logo ayarları bulunamadı.");
            // ── e-Arşiv: tek gün kabul ediyor → gün gün çek ──
            if (documentType == ELogoDocumentType.EArchive)
            {
                List<DateTime> days = new List<DateTime>();
                for (var day = begin.Date; day <= end.Date; day = day.AddDays(1))
                    days.Add(day);
                if (days.Count > 31)
                    days = days.Take(31).ToList();
                SemaphoreSlim semaphore = new SemaphoreSlim(5);
                var tasks = days.Select(async day =>
                {
                    await semaphore.WaitAsync();
                    try
                    {
                        return await GetDocumentListSingleDayAsync(
                            session, settings, day, day, documentType);
                    }
                    finally { semaphore.Release(); }
                });
                var results = await Task.WhenAll(tasks);
                // ── Hata olan günleri atla, gerisini birleştir ──
                var allItems = results
                    .Where(r => r.Success && r.Items != null)
                    .SelectMany(r => r.Items!)
                    .ToList();
                int hataGunler = results.Count(r => !r.Success);
                if (hataGunler > 0)
                    _logger.LogWarning(
                        "e-Arşiv listesi: {Hata} gün hata aldı, {Basarili} gün başarılı.",
                        hataGunler, results.Length - hataGunler);
                _logger.LogInformation(
                    "GetDocumentList [EARCHIVE] {Begin:yyyy-MM-dd}~{End:yyyy-MM-dd} " +
                    "toplam {Count} belge.",
                    begin, end, allItems.Count);
                // En az bir gün başarılıysa success=true döndür
                bool anySuccess = results.Any(r => r.Success);
                return (anySuccess || allItems.Count == 0, allItems, null);
            }
            // ── e-Fatura: normal tarih aralığı, max 30 gün ──
            if ((end - begin).TotalDays > 30)
                end = begin.AddDays(30);
            var result = await GetDocumentListSingleDayAsync(
                session, settings, begin, end, documentType);
            return (result.Success, result.Items, result.Error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ELogo GetDocumentList hatası");
            return (false, null, ex.Message);
        }
    }
    // ── TEK GÜN / ARALIK İSTEĞİ (iç metot) ───────────────
    private async Task<(bool Success, List<ELogoInvoiceItem>? Items, string? Error)>
        GetDocumentListSingleDayAsync(
            string session,
            ELogoSettings settings,
            DateTime begin,
            DateTime end,
            ELogoDocumentType documentType)
    {
        string docType = ToDocTypeString(documentType);
        string soap = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
  <soap:Body>
    <GetDocumentList xmlns=""http://tempuri.org/"">
      <sessionID>{session}</sessionID>
      <paramList xmlns:arr=""http://schemas.microsoft.com/2003/10/Serialization/Arrays"">
        <arr:string>DOCUMENTTYPE={docType}</arr:string>
        <arr:string>BEGINDATE={begin:yyyy-MM-dd}</arr:string>
        <arr:string>ENDDATE={end:yyyy-MM-dd}</arr:string>
        <arr:string>OPTYPE=1</arr:string>
        <arr:string>DATEBY=1</arr:string>
      </paramList>
    </GetDocumentList>
  </soap:Body>
</soap:Envelope>";

        try
        {
            string raw = await SoapPostAsync(soap, "GetDocumentList", settings.Url);
            var xdoc = XDocument.Parse(raw);
            XNamespace a = "http://schemas.datacontract.org/2004/07/eFaturaWebService";
            XNamespace b = "http://schemas.microsoft.com/2003/10/Serialization/Arrays";
            string? resultCode = xdoc.Descendants(a + "resultCode").FirstOrDefault()?.Value;
            if (resultCode == "-1")
            {
                string? msg = xdoc.Descendants(a + "resultMsg").FirstOrDefault()?.Value;
                _logger.LogWarning(
                    "GetDocumentList [{DocType}] {Date:yyyy-MM-dd} hata: {Msg}",
                    docType, begin, msg);
                return (false, null, msg ?? "e-Logo hata döndürdü.");
            }
            var items = xdoc.Descendants(a + "Document")
                .Select(d => new ELogoInvoiceItem
                {
                    DocumentUuid = d.Element(a + "documentUuid")?.Value,
                    DocumentId = d.Element(a + "documentId")?.Value,
                    DocInfo = d.Element(a + "docInfo") != null
                        ? d.Element(a + "docInfo")!
                            .Elements(b + "string")
                            .Select(x => x.Value)
                            .ToArray()
                        : null,
                    DocumentType = documentType
                })
                .Where(x => !string.IsNullOrEmpty(x.DocumentUuid))
                .ToList();
            return (true, items, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "GetDocumentListSingleDay hata. DocType: {DocType} Tarih: {Date:yyyy-MM-dd}",
                docType, begin);
            return (false, null, ex.Message);
        }
    }
    // ── UBL ÇEK ──────────────────────────────────────────
    public async Task<(bool Success, XDocument? Ubl, string? Error)>
        GetInvoiceUblAsync(
            string session,
            string uuid,
            ELogoDocumentType documentType = ELogoDocumentType.EInvoice)
    {
        try
        {
            ELogoSettings? settings = await GetSettingsAsync();
            if (settings == null)
                return (false, null, "e-Logo ayarları bulunamadı.");
            string docType = ToDocTypeString(documentType);
            string soap = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
  <soap:Body>
    <GetDocumentData xmlns=""http://tempuri.org/"">
      <sessionID>{session}</sessionID>
      <uuid>{uuid}</uuid>
      <paramList xmlns:arr=""http://schemas.microsoft.com/2003/10/Serialization/Arrays"">
        <arr:string>DOCUMENTTYPE={docType}</arr:string>
        <arr:string>DATAFORMAT=UBL</arr:string>
      </paramList>
    </GetDocumentData>
  </soap:Body>
</soap:Envelope>";
            string raw = await SoapPostAsync(soap, "GetDocumentData", settings.Url);
            var resp = XDocument.Parse(raw);
            XNamespace a = "http://schemas.datacontract.org/2004/07/eFaturaWebService";
            string? b64 = resp.Descendants(a + "Value").FirstOrDefault()?.Value;
            if (string.IsNullOrEmpty(b64))
                return (false, null, "UBL verisi boş döndü.");
            byte[] zip = Convert.FromBase64String(b64);
            using MemoryStream ms = new MemoryStream(zip);
            using ZipArchive za = new ZipArchive(ms, ZipArchiveMode.Read);
            foreach (ZipArchiveEntry entry in za.Entries)
            {
                if (entry.Name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                {
                    using Stream s = entry.Open();
                    return (true, XDocument.Load(s), null);
                }
            }
            return (false, null, "ZIP içinde XML bulunamadı.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ELogo GetInvoiceUbl hatası. UUID: {Uuid}", uuid);
            return (false, null, ex.Message);
        }
    }
    // ── GÖRSEL ÇEK ────────────────────────────────────────
    public async Task<(bool Success, byte[]? Data, string? Error)>
        GetInvoiceVisualAsync(
            string session,
            string uuid,
            VisualFormat format,
            ELogoDocumentType documentType = ELogoDocumentType.EInvoice)
    {
        try
        {
            ELogoSettings? settings = await GetSettingsAsync();
            if (settings == null)
                return (false, null, "e-Logo ayarları bulunamadı.");
            string docType = ToDocTypeString(documentType);
            string dataFormat = format == VisualFormat.Pdf ? "PDF" : "HTML";
            string soap = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
  <soap:Body>
    <GetDocumentData xmlns=""http://tempuri.org/"">
      <sessionID>{session}</sessionID>
      <uuid>{uuid}</uuid>
      <paramList xmlns:arr=""http://schemas.microsoft.com/2003/10/Serialization/Arrays"">
        <arr:string>DOCUMENTTYPE={docType}</arr:string>
        <arr:string>DATAFORMAT={dataFormat}</arr:string>
      </paramList>
    </GetDocumentData>
  </soap:Body>
</soap:Envelope>";
            string raw = await SoapPostAsync(soap, "GetDocumentData", settings.Url);
            var resp = XDocument.Parse(raw);
            XNamespace a = "http://schemas.datacontract.org/2004/07/eFaturaWebService";
            string? b64 = resp.Descendants(a + "Value").FirstOrDefault()?.Value;
            if (string.IsNullOrEmpty(b64))
                return (false, null, "Görsel verisi boş döndü.");
            byte[] zip = Convert.FromBase64String(b64);
            using MemoryStream ms = new MemoryStream(zip);
            using ZipArchive za = new ZipArchive(ms, ZipArchiveMode.Read);
            string ext = format == VisualFormat.Pdf ? ".pdf" : ".html";
            foreach (ZipArchiveEntry entry in za.Entries)
            {
                if (entry.Name.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                {
                    using Stream s = entry.Open();
                    using MemoryStream outMs = new MemoryStream();
                    await s.CopyToAsync(outMs);
                    return (true, outMs.ToArray(), null);
                }
            }
            return (false, null, $"ZIP içinde {ext} bulunamadı.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ELogo GetInvoiceVisual hatası. UUID: {Uuid}", uuid);
            return (false, null, ex.Message);
        }
    }

    // ── DURUM ÇEK ─────────────────────────────────────────
    public async Task<(bool Success, ELogoDocumentStatus? Status, string? Error)>
        GetInvoiceStatusAsync(
            string session,
            string uuid,
            ELogoDocumentType documentType = ELogoDocumentType.EInvoice)
    {
        try
        {
            ELogoSettings? settings = await GetSettingsAsync();
            if (settings == null)
                return (false, null, "e-Logo ayarları bulunamadı.");
            string docType = ToDocTypeString(documentType);
            string soap = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
  <soap:Body>
    <GetDocumentStatus xmlns=""http://tempuri.org/"">
      <sessionID>{session}</sessionID>
      <uuid>{uuid}</uuid>
      <paramList xmlns:arr=""http://schemas.microsoft.com/2003/10/Serialization/Arrays"">
        <arr:string>DOCUMENTTYPE={docType}</arr:string>
      </paramList>
    </GetDocumentStatus>
  </soap:Body>
</soap:Envelope>";
            string raw = await SoapPostAsync(soap, "GetDocumentStatus", settings.Url);
            var xdoc = XDocument.Parse(raw);
            XNamespace a = "http://schemas.datacontract.org/2004/07/eFaturaWebService";
            var si = xdoc.Descendants(a + "statusInfo").FirstOrDefault();
            if (si == null)
                return (false, null, "Durum bilgisi alınamadı.");
            ELogoDocumentStatus status = new ELogoDocumentStatus
            {
                Status = int.TryParse(si.Element(a + "status")?.Value, out int s) ? s : 0,
                Code = int.TryParse(si.Element(a + "code")?.Value, out int c) ? c : 0,
                Description = si.Element(a + "description")?.Value,
                EnvelopeId = si.Element(a + "envelopeId")?.Value,
                IsCancel = si.Element(a + "isCancel")?.Value == "true"
            };
            return (true, status, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ELogo GetInvoiceStatus hatası. UUID: {Uuid}", uuid);
            return (false, null, ex.Message);
        }
    }
    // ── SOAP HELPER ───────────────────────────────────────
    private async Task<string> SoapPostAsync(string soap, string action, string url)
    {
        StringContent? content = new StringContent(soap, Encoding.UTF8, "text/xml");
        content.Headers.Add("SOAPAction",
            $"\"http://tempuri.org/IPostBoxService/{action}\"");
        HttpResponseMessage resp = await _http.PostAsync(url, content);
        return await resp.Content.ReadAsStringAsync();
    }
}