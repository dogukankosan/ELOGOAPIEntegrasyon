namespace EBelgeAPI.Models.Entities;

public class LogoSettings
{
    public int Id { get; set; }
    public string ServerUrl { get; set; } = null!;
    public string MachineId { get; set; } = null!;
    public string FirmNr { get; set; } = null!;
    public string ClientId { get; set; } = null!;
    public string ClientSecret { get; set; } = null!;
    public string CustomerId { get; set; } = null!;
    public string LogoUsername { get; set; } = null!;
    public string LogoPassword { get; set; } = null!;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}