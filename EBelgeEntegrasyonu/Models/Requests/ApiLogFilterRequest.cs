namespace EBelgeAPI.Models.Requests;

public class ApiLogFilterRequest
{
    public string? Level { get; set; }
    public string? Source { get; set; }
    public string? Username { get; set; }
    public string? Path { get; set; }
    public int? StatusCode { get; set; }
    public DateTime? BaslangicTarihi { get; set; }
    public DateTime? BitisTarihi { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}