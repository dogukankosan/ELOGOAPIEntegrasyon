namespace EBelgeAPI.Models.Entities;

public class RevokedToken
{
    public long Id { get; set; }
    public string Jti { get; set; } = null!;
    public string Username { get; set; } = null!;
    public DateTime ExpiresAt { get; set; }
    public DateTime RevokedAt { get; set; }
}