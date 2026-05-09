namespace EBelgeAPI.Models.Entities;

public class ELogoSettings
{
    public int Id { get; set; }
    public string Url { get; set; } = null!;
    public string Username { get; set; } = null!;
    public string Password { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}