namespace EBelgeAPI.Models.Entities;

public class CariFiltre
{
    public int Id { get; set; }
    public string KimlikNo { get; set; } = "";
    public string KimlikTipi { get; set; } = ""; // VKN veya TCKN
    public string? Ad { get; set; }
    public string? Aciklama { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}