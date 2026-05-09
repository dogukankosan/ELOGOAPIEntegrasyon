namespace EBelgeAPI.Models.Entities;

public class LogoTokenCache
{
    public int Id { get; set; }
    public string AccessToken { get; set; } = null!;
    public DateTime ExpireDate { get; set; }
    public string Server { get; set; } = null!;
    public string Firm { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}