using EBelgeAPI.Services;

namespace EBelgeAPI.Models.ELogo;

public class ELogoInvoiceItem
{
    public string? DocumentUuid { get; set; }
    public string? DocumentId { get; set; }
    public string[]? DocInfo { get; set; }
    public ELogoDocumentType DocumentType { get; set; } = ELogoDocumentType.EInvoice;
}

public class ELogoInvoiceListResponse
{
    public List<ELogoInvoiceItem> Items { get; set; } = new();
    public bool Success { get; set; }
    public string? Error { get; set; }
}