namespace EBelgeUI.Models;
public class CariFiltereDto
{
    public int Id { get; set; }
    public string KimlikNo { get; set; } = "";
    public string KimlikTipi { get; set; } = ""; // VKN veya TCKN
    public string? Ad { get; set; }
    public string? Aciklama { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}
public class CariFiltereIndexViewModel
{
    public List<CariFiltereDto> Filtreler { get; set; } = new();
    public string? ErrorMessage { get; set; }
}
public class CariFiltereApiResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public List<CariFiltereDto>? Data { get; set; }
}