namespace EBelgeUI.Models;

public class SalesInvoiceFilterViewModel
{
    public DateTime? BaslangicTarihi { get; set; }
    public DateTime? BitisTarihi { get; set; }
    public string? FaturaNo { get; set; }
    public string? AliciVkn { get; set; }
    public string? AliciUnvan { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;

    // Transfer filtreleri
    public string? TransferDurumu { get; set; }
    public bool? AmbarSecilmemis { get; set; }
    public bool? SatisElemaniSecilmemis { get; set; }
}
public class SalesInvoiceIndexViewModel
{
    public HashSet<string> AktarilmisUuidler { get; set; } = new();
    public Dictionary<string, int> FaturaAmbarMap { get; set; } = new();
    public Dictionary<string, string> FaturaSatisElemaniMap { get; set; } = new();
    public List<AmbarDto> Ambarlar { get; set; } = new();
    public List<SatisElemaniDto> SatisElemanlari { get; set; } = new();
    public SalesInvoiceFilterViewModel Filter { get; set; } = new();
    public List<SalesInvoiceDto> Invoices { get; set; } = [];
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public string? ErrorMessage { get; set; }
    public Dictionary<string, string> HataMap { get; set; } = new();
}
// Yeni response class:
public class HataMapResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public Dictionary<string, string>? Data { get; set; }
}
public class SalesInvoiceDto
{
    public string? Uuid { get; set; }
    public string? FaturaNo { get; set; }
    public DateTime? FaturaTarihi { get; set; }
    public string? AliciUnvan { get; set; }
    public string? AliciVkn { get; set; }
    public string? AliciTckn { get; set; }
    public decimal Matrah { get; set; }
    public decimal KdvTutar { get; set; }
    public decimal GenelToplam { get; set; }
    public string? ParaBirimi { get; set; }
    public int GibDurumKodu { get; set; }
    public string? GibDurumAciklama { get; set; }
    public bool IptalMi { get; set; }
    public string DocType { get; set; } = "einvoice";   // ← "einvoice" | "earchive"
    public string FaturaTipi { get; set; } = "SATIS";   // ← "SATIS" | "IADE"
    public bool TemlikVar { get; set; }                  // ← Vodafone temlik
}
public class AmbarDto
{
    public int Id { get; set; }
    public string Kod { get; set; } = "";
    public string Ad { get; set; } = "";
    public bool IsActive { get; set; }
}
public class SatisElemaniDto
{
    public int Reference { get; set; }
    public string Code { get; set; } = "";
    public string Description { get; set; } = "";
}
public class SalesInvoiceApiResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public List<SalesInvoiceDto>? Data { get; set; }
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}
public class SalesInvoiceTransferredResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public List<string>? Data { get; set; }
}
public class SalesInvoiceDetailApiResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public SalesInvoiceDto? Data { get; set; }
}
public class AmbarApiResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public List<AmbarDto>? Data { get; set; }
}
public class SatisElemaniApiResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public List<SatisElemaniDto>? Data { get; set; }
}
// ── Transfer request'lere DocType eklendi ─────────────
public class TransferRequest
{
    public string AmbarKodu { get; set; } = "";
    public string SatisElemaniKodu { get; set; } = "";
    public string DocType { get; set; } = "einvoice";   // ← eklendi
}
// YENİ:
public class TopluTransferItem
{
    public string Uuid { get; set; } = "";
    public string DocType { get; set; } = "einvoice";
    public string AmbarKodu { get; set; } = "";
    public string SatisElemaniKodu { get; set; } = "";
}
public class TopluTransferRequest
{
    public List<TopluTransferItem> Items { get; set; } = new();

}
public class TransferResultDto
{
    public string? Uuid { get; set; }
    public string? ELogoFaturaNo { get; set; }
    public bool Success { get; set; }
    public string? LogoFaturaNo { get; set; }
    public int? LogoLogicalRef { get; set; }
    public string? Error { get; set; }
}
public class TransferApiResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public TransferResultDto? Data { get; set; }
}
public class TopluTransferApiResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public List<TransferResultDto>? Data { get; set; }
}
public class FaturaAmbarMapResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public Dictionary<string, int>? Data { get; set; }
}
public class FaturaSatisElemaniMapResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public Dictionary<string, string>? Data { get; set; }
}