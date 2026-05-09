namespace EBelgeAPI.Models.Entities;

public class ApiLog
{
    public long Id { get; set; }
    public string Level { get; set; } = null!;
    public string Source { get; set; } = null!;
    public string? Method { get; set; }
    public string? Path { get; set; }
    public int? StatusCode { get; set; }
    public string Message { get; set; } = null!;
    public string? Detail { get; set; }
    public string? Username { get; set; }
    public string? IpAddress { get; set; }
    public int? Duration { get; set; }
    public DateTime CreatedAt { get; set; }
}