using EBelgeAPI.Models.DTOs;
public class SalesInvoiceDto
{
    public string? Uuid { get; set; }
    public string? FaturaNo { get; set; }
    public DateTime? FaturaTarihi { get; set; }
    public string? AliciUnvan { get; set; }
    public string? AliciVkn { get; set; }
    public string? AliciTckn { get; set; }
    public string? AliciTelefon { get; set; }
    public string? AliciIlce { get; set; }  // town2
    public string? AliciIl { get; set; }  // city2
    public string? AliciUlke { get; set; }  // country
    public decimal Matrah { get; set; }
    public decimal KdvTutar { get; set; }
    public decimal GenelToplam { get; set; }
    public string? ParaBirimi { get; set; }
    public string? FaturaNotu { get; set; }
    public int GibDurumKodu { get; set; }
    public string? GibDurumAciklama { get; set; }
    public bool IptalMi { get; set; }
    public string DocType { get; set; } = "einvoice";   // "einvoice" | "earchive"
    public string FaturaTipi { get; set; } = "SATIS";   // "SATIS" | "IADE"
    public bool TemlikVar { get; set; }                  // Vodafone temlik notu
    public List<SalesInvoiceLineDto> Kalemler { get; set; } = new();
}