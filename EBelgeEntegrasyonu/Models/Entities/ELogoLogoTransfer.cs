namespace EBelgeAPI.Models.Entities;

public class ELogoLogoTransfer
{
    public int Id { get; set; }
    public string ELogoUuid { get; set; } = "";
    public string? ELogoFaturaNo { get; set; }
    public string? LogoFaturaNo { get; set; }
    public int? LogoLogicalRef { get; set; }
    public int AktarimDurumu { get; set; } = 0; // 0=Bekliyor, 1=Aktarıldı, 2=Hata
    public string? HataMesaji { get; set; }
    public DateTime? AktarimTarihi { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}