namespace EBelgeAPI.Models.Requests;

public class SalesInvoiceListRequest
{
    public string? FaturaNo { get; set; }
    public string? AliciVkn { get; set; }
    public string? AliciUnvan { get; set; }
    public DateTime? BaslangicTarihi { get; set; }
    public DateTime? BitisTarihi { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}