namespace EBelgeAPI.Models.DTOs;

public class LogoTokenDto
{
    public string AccessToken { get; set; } = null!;
    public DateTime ExpireDate { get; set; }
    public string Server { get; set; } = null!;
    public string Firm { get; set; } = null!;
    public int RemainingMinutes { get; set; }
}