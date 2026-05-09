using EBelgeAPI.Data.Interfaces;
using EBelgeAPI.Models.DTOs;
using EBelgeAPI.Models.ELogo;
using EBelgeAPI.Models.Requests;
using EBelgeAPI.Services.Interfaces;
using System.Xml.Linq;

namespace EBelgeAPI.Services;

public class SalesInvoiceService : ISalesInvoiceService
{
    private readonly IELogoService _eLogo;
    private readonly ILogger<SalesInvoiceService> _logger;
    private readonly ICariFilterRepository _cariFilterRepo;
    static readonly XNamespace Cbc =
        "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";
    static readonly XNamespace Cac =
        "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
    public SalesInvoiceService(
        IELogoService eLogo,
        ICariFilterRepository cariFilterRepo,
        ILogger<SalesInvoiceService> logger)
    {
        _eLogo = eLogo;
        _cariFilterRepo = cariFilterRepo;
        _logger = logger;
    }
    // ── RAW UBL ───────────────────────────────────────────
    public async Task<(bool Success, string? Xml, string? Error)>
        GetSalesInvoiceUblRawAsync(
            string uuid,
            ELogoDocumentType docType = ELogoDocumentType.EInvoice)
    {
        var (loginOk, session, loginErr) = await _eLogo.LoginAsync();
        if (!loginOk) return (false, null, loginErr);
        try
        {
            var (ok, ubl, err) = await _eLogo.GetInvoiceUblAsync(session!, uuid, docType);
            if (!ok || ubl == null) return (false, null, err);
            return (true, ubl.ToString(), null);
        }
        finally { await _eLogo.LogoutAsync(session!); }
    }
    // ── LİSTE ─────────────────────────────────────────────
    public async Task<(bool Success, List<SalesInvoiceDto>? Data, int TotalCount, string? Error)>
        GetSalesInvoicesAsync(SalesInvoiceListRequest request)
    {
        if (!request.BaslangicTarihi.HasValue || !request.BitisTarihi.HasValue)
            return (false, null, 0, "Başlangıç ve bitiş tarihi zorunludur.");
        if ((request.BitisTarihi.Value - request.BaslangicTarihi.Value).TotalDays > 30)
            return (false, null, 0, "Tarih aralığı en fazla 30 gün olabilir.");
        var (loginOk, session, loginErr) = await _eLogo.LoginAsync();
        if (!loginOk) return (false, null, 0, loginErr);
        try
        {
            DateTime begin = request.BaslangicTarihi.Value;
            DateTime end = request.BitisTarihi.Value;
            // ── e-Fatura + e-Arşiv listelerini paralel çek ──
            var eInvoiceTask = _eLogo.GetInvoiceListByDateAsync(
                session!, begin, end, ELogoDocumentType.EInvoice);
            var eArchiveTask = _eLogo.GetInvoiceListByDateAsync(
                session!, begin, end, ELogoDocumentType.EArchive);
            await Task.WhenAll(eInvoiceTask, eArchiveTask);
            var (_, invItems, _) = eInvoiceTask.Result;
            var (_, arcItems, _) = eArchiveTask.Result;
            // ── Cari filtre setini önceden al ──
            var filtreSet = await _cariFilterRepo.GetActiveKimlikNoSetAsync();
            // ── e-Arşiv: docInfo'dan hızlı filtrele, sonra UBL çek ──
            var arcItemList = (arcItems ?? new())
                .Where(x => !string.IsNullOrEmpty(x.DocumentUuid))
                .Where(x =>
                {
                    var info = ParseDocInfo(x.DocInfo);
                    string? kimlik = info.GetValueOrDefault("DocCustomerVknTckn")?.Trim() ?? "";
                    // Cari kara liste
                    if (!string.IsNullOrEmpty(kimlik) && filtreSet.Contains(kimlik)) return false;
                    // VKN/TC filtresi
                    if (!string.IsNullOrWhiteSpace(request.AliciVkn))
                        return kimlik.Contains(request.AliciVkn.Trim(), StringComparison.OrdinalIgnoreCase);
                    // FaturaNo filtresi
                    if (!string.IsNullOrWhiteSpace(request.FaturaNo))
                    {
                        string? faturaNo = info.GetValueOrDefault("DocInvoiceId")?.Trim() ?? "";
                        return faturaNo.Contains(request.FaturaNo.Trim(), StringComparison.OrdinalIgnoreCase);
                    }

                    return true;
                })
                .ToList();
            List<SalesInvoiceDto> archiveDtos = new();
            if (arcItemList.Count > 0)
            {
                SemaphoreSlim semArc = new SemaphoreSlim(5);
                var arcTasks = arcItemList.Select(async item =>
                {
                    await semArc.WaitAsync();
                    try
                    {
                        var (ok, ubl, _) = await _eLogo.GetInvoiceUblAsync(
                            session!, item.DocumentUuid!, ELogoDocumentType.EArchive);
                        if (!ok || ubl == null)
                        {
                            _logger.LogWarning("e-Arşiv UBL alınamadı. UUID: {Uuid}",
                                item.DocumentUuid);
                            return null;
                        }
                        return ParseUblToDto(item.DocumentUuid!, ubl, null,
                            ELogoDocumentType.EArchive);
                    }
                    finally { semArc.Release(); }
                });
                SalesInvoiceDto?[] arcResults = await Task.WhenAll(arcTasks);
                archiveDtos = arcResults.Where(d => d != null).Select(d => d!).ToList();
            }
            // ── e-Fatura: UBL çek ──
            var invoiceItems = (invItems ?? new())
                .Where(x => !string.IsNullOrEmpty(x.DocumentUuid))
                .ToList();
            List<SalesInvoiceDto> invoiceDtos = new();
            if (invoiceItems.Count > 0)
            {
                SemaphoreSlim semaphore = new SemaphoreSlim(5);
                var tasks = invoiceItems.Select(async item =>
                {
                    await semaphore.WaitAsync();
                    try
                    {
                        var (ok, ubl, _) = await _eLogo.GetInvoiceUblAsync(
                            session!, item.DocumentUuid!, ELogoDocumentType.EInvoice);
                        if (!ok || ubl == null)
                        {
                            _logger.LogWarning("e-Fatura UBL alınamadı. UUID: {Uuid}",
                                item.DocumentUuid);
                            return null;
                        }
                        return ParseUblToDto(item.DocumentUuid!, ubl, null,
                            ELogoDocumentType.EInvoice);
                    }
                    finally { semaphore.Release(); }
                });
                SalesInvoiceDto?[] results = await Task.WhenAll(tasks);
                invoiceDtos = results.Where(d => d != null).Select(d => d!).ToList();
            }
            var dtos = invoiceDtos.Concat(archiveDtos).ToList();
            if (dtos.Count == 0)
                return (true, new List<SalesInvoiceDto>(), 0, null);
            // ── Cari filtre (e-Fatura için) ──
            dtos = dtos.Where(d =>
            {
                if (!string.IsNullOrEmpty(d.AliciVkn) && filtreSet.Contains(d.AliciVkn)) return false;
                if (!string.IsNullOrEmpty(d.AliciTckn) && filtreSet.Contains(d.AliciTckn)) return false;
                return true;
            }).ToList();
            // ── FaturaNo filtresi ──
            if (!string.IsNullOrWhiteSpace(request.FaturaNo))
                dtos = dtos.Where(d =>
                    d.FaturaNo != null &&
                    d.FaturaNo.Contains(request.FaturaNo,
                        StringComparison.OrdinalIgnoreCase)).ToList();
            // ── VKN/TC filtresi ──
            if (!string.IsNullOrWhiteSpace(request.AliciVkn))
                dtos = dtos.Where(d =>
                    (d.AliciVkn != null && d.AliciVkn.Contains(request.AliciVkn, StringComparison.OrdinalIgnoreCase)) ||
                    (d.AliciTckn != null && d.AliciTckn.Contains(request.AliciVkn, StringComparison.OrdinalIgnoreCase))
                ).ToList();
            // ── Ünvan filtresi ──
            if (!string.IsNullOrWhiteSpace(request.AliciUnvan))
                dtos = dtos.Where(d =>
                    d.AliciUnvan != null &&
                    d.AliciUnvan.Contains(request.AliciUnvan,
                        StringComparison.OrdinalIgnoreCase)).ToList();
            int totalCount = dtos.Count;
            var paged = dtos
                .OrderByDescending(d => d.FaturaTarihi)
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToList();
            return (true, paged, totalCount, null);
        }
        finally { await _eLogo.LogoutAsync(session!); }
    }
    // ── DETAY ─────────────────────────────────────────────
    public async Task<(bool Success, SalesInvoiceDto? Data, string? Error)>
        GetSalesInvoiceDetailAsync(
            string uuid,
            ELogoDocumentType docType = ELogoDocumentType.EInvoice)
    {
        var (loginOk, session, loginErr) = await _eLogo.LoginAsync();
        if (!loginOk) return (false, null, loginErr);
        try
        {
            var ublTask = _eLogo.GetInvoiceUblAsync(session!, uuid, docType);
            var statusTask = _eLogo.GetInvoiceStatusAsync(session!, uuid, docType);
            await Task.WhenAll(ublTask, statusTask);
            var (ublOk, ubl, _) = ublTask.Result;
            var (statusOk, status, _) = statusTask.Result;
            if (!ublOk || ubl == null)
                return (false, null, "Fatura UBL verisi alınamadı.");
            SalesInvoiceDto? dto = ParseUblToDto(uuid, ubl, statusOk ? status : null, docType);
            return (true, dto, null);
        }
        finally { await _eLogo.LogoutAsync(session!); }
    }
    // ── GÖRSEL ────────────────────────────────────────────
    public async Task<(bool Success, byte[]? Data, string ContentType, string? Error)>
        GetSalesInvoiceVisualAsync(
            string uuid,
            VisualFormat format,
            ELogoDocumentType docType = ELogoDocumentType.EInvoice)
    {
        var (loginOk, session, loginErr) = await _eLogo.LoginAsync();
        if (!loginOk) return (false, null, "", loginErr);
        try
        {
            var (ok, data, err) =
                await _eLogo.GetInvoiceVisualAsync(session!, uuid, format, docType);
            if (!ok) return (false, null, "", err);
            string contentType = format == VisualFormat.Pdf
                ? "application/pdf"
                : "text/html; charset=utf-8";
            return (true, data, contentType, null);
        }
        finally { await _eLogo.LogoutAsync(session!); }
    }
    // ── PRIVATE: UBL → DTO ───────────────────────────────
    private SalesInvoiceDto ParseUblToDto(
        string uuid,
        XDocument ubl,
        ELogoDocumentStatus? status,
        ELogoDocumentType docType = ELogoDocumentType.EInvoice)
    {
        var root = ubl.Root!;
        // ── Fatura tipi ──
        string faturaTipi = root.Element(Cbc + "InvoiceTypeCode")?.Value ?? "SATIS";
        // ── ProfileID → DocType ──
        string profileId = root.Element(Cbc + "ProfileID")?.Value ?? "";
        string dtoDocType = profileId.Contains("EARSIV", StringComparison.OrdinalIgnoreCase)
            ? "earchive" : "einvoice";
        // ── Temlik kontrolü ──
        bool temlikVar = root.Elements(Cbc + "Note")
            .Any(n => n.Value.Contains("Vodafone", StringComparison.OrdinalIgnoreCase)
                   && n.Value.Contains("Temlik", StringComparison.OrdinalIgnoreCase));
        // ── Alıcı bilgileri ──
        string aliciVkn = GetPartyId(root, Cac + "AccountingCustomerParty", "VKN");
        string aliciTckn = GetPartyId(root, Cac + "AccountingCustomerParty", "TCKN");
        string? aliciUnvan = root
            .Element(Cac + "AccountingCustomerParty")
            ?.Element(Cac + "Party")
            ?.Element(Cac + "PartyName")
            ?.Element(Cbc + "Name")?.Value;
        if (string.IsNullOrEmpty(aliciUnvan))
        {
            var person = root
                .Element(Cac + "AccountingCustomerParty")
                ?.Element(Cac + "Party")
                ?.Element(Cac + "Person");
            string? ad = person?.Element(Cbc + "FirstName")?.Value;
            string? soyad = person?.Element(Cbc + "FamilyName")?.Value;
            if (!string.IsNullOrEmpty(ad) || !string.IsNullOrEmpty(soyad))
                aliciUnvan = $"{ad} {soyad}".Trim();
        }
        // ── Tutarlar ──
        decimal matrah = ParseDecimal(root
            .Element(Cac + "LegalMonetaryTotal")
            ?.Element(Cbc + "TaxExclusiveAmount")?.Value);
        decimal kdv = ParseDecimal(root
            .Element(Cac + "TaxTotal")
            ?.Element(Cbc + "TaxAmount")?.Value);
        decimal toplam = ParseDecimal(root
            .Element(Cac + "LegalMonetaryTotal")
            ?.Element(Cbc + "PayableAmount")?.Value);
        // ── Tarih: IssueDate + IssueTime ──
        string? issueDate = root.Element(Cbc + "IssueDate")?.Value;
        string? issueTime = root.Element(Cbc + "IssueTime")?.Value?.Split('.')[0];
        DateTime? tarih = null;
        if (!string.IsNullOrEmpty(issueDate))
        {
            string dateTimeStr = string.IsNullOrEmpty(issueTime)
                ? issueDate
                : $"{issueDate}T{issueTime}";
            if (DateTime.TryParse(dateTimeStr, out DateTime dt))
                tarih = dt;
        }
        // ── Kalemler ──
        var kalemler = root.Elements(Cac + "InvoiceLine").Select((line, idx) =>
        {
            var item = line.Element(Cac + "Item");
            // Seri no: kalem bazında cbc:Note
            string? seriNo = line.Element(Cbc + "Note")?.Value?.Trim();
            if (string.IsNullOrWhiteSpace(seriNo)) seriNo = null;
            // Malzeme kodları
            string? barkod = item
                ?.Element(Cac + "SellersItemIdentification")
                ?.Element(Cbc + "ID")?.Value?.Trim();
            string? manufacturerKodu = item
                ?.Element(Cac + "ManufacturersItemIdentification")
                ?.Element(Cbc + "ID")?.Value?.Trim();
            string? urunAdi = item?.Element(Cbc + "Name")?.Value?.Trim();
            string? aciklama = item?.Element(Cbc + "Description")?.Value?.Trim();
            // İskonto
            decimal iskontoTutar = 0;
            var allowance = line.Element(Cac + "AllowanceCharge");
            if (allowance != null)
            {
                string? chargeIndicator = allowance.Element(Cbc + "ChargeIndicator")?.Value;
                if (chargeIndicator?.Equals("false", StringComparison.OrdinalIgnoreCase) == true)
                    iskontoTutar = ParseDecimal(allowance.Element(Cbc + "Amount")?.Value);
            }
            return new SalesInvoiceLineDto
            {
                LineNo = idx + 1,
                Barkod = barkod,
                ManufacturerKodu = manufacturerKodu,
                UrunAdi = urunAdi,
                Aciklama = aciklama,
                Miktar = ParseDecimal(line.Element(Cbc + "InvoicedQuantity")?.Value),
                BirimKodu = line.Element(Cbc + "InvoicedQuantity")
                                       ?.Attribute("unitCode")?.Value,
                BirimFiyat = ParseDecimal(line
                                       .Element(Cac + "Price")
                                       ?.Element(Cbc + "PriceAmount")?.Value),
                KdvOrani = ParseDecimal(line
                                       .Element(Cac + "TaxTotal")
                                       ?.Element(Cac + "TaxSubtotal")
                                       ?.Element(Cbc + "Percent")?.Value),
                KdvTutar = ParseDecimal(line
                                       .Element(Cac + "TaxTotal")
                                       ?.Element(Cbc + "TaxAmount")?.Value),
                SatirToplam = ParseDecimal(line
                                       .Element(Cbc + "LineExtensionAmount")?.Value),
                IskontoTutar = iskontoTutar,
                SeriNo = seriNo
            };
        }).ToList();
        // Fatura bazındaki Note'ları birleştir (kalem Note'ları değil)
        var faturaNotlari = root.Elements(Cbc + "Note")
            .Select(n => n.Value.Trim())
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .ToList();
        string? faturaNotu = faturaNotlari.Count > 0
            ? string.Join(" | ", faturaNotlari)
            : null;
        return new SalesInvoiceDto
        {
            Uuid = uuid,
            FaturaNo = root.Element(Cbc + "ID")?.Value,
            FaturaTarihi = tarih,
            AliciVkn = aliciVkn,
            AliciTckn = aliciTckn,
            AliciUnvan = aliciUnvan,
            FaturaNotu=faturaNotu,
            Matrah = matrah,
            KdvTutar = kdv,
            GenelToplam = toplam,
            ParaBirimi = root.Element(Cbc + "DocumentCurrencyCode")?.Value ?? "TRY",
            GibDurumKodu = status?.Code ?? 0,
            GibDurumAciklama = status?.Description,
            IptalMi = status?.IsCancel ?? false,
            DocType = dtoDocType,
            FaturaTipi = faturaTipi,
            TemlikVar = temlikVar,
            Kalemler = kalemler
        };
    }
    // ── HELPERS ───────────────────────────────────────────
    private static string GetPartyId(XElement root, XName partyParent, string schemeId)
    {
        var party = root.Element(partyParent)?.Element(Cac + "Party");
        if (party == null) return "";
        foreach (var pi in party.Elements(Cac + "PartyIdentification"))
        {
            var id = pi.Element(Cbc + "ID");
            if (id?.Attribute("schemeID")?.Value == schemeId)
                return id.Value;
        }
        return "";
    }
    private static decimal ParseDecimal(string? value) =>
        decimal.TryParse(value,
            System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture,
            out decimal result) ? result : 0;
    private static Dictionary<string, string> ParseDocInfo(string[]? docInfo)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (docInfo == null) return dict;
        foreach (string entry in docInfo)
        {
            int idx = entry.IndexOf('=');
            if (idx > 0)
                dict[entry[..idx].Trim()] = entry[(idx + 1)..].Trim();
        }
        return dict;
    }
}