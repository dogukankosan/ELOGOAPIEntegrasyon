namespace EBelgeAPI.Models.DTOs;

public class SalesTransferResultDto
{
    public string? Uuid { get; set; }
    public string? ELogoFaturaNo { get; set; }
    public bool Success { get; set; }
    public string? LogoFaturaNo { get; set; }
    public int? LogoLogicalRef { get; set; }
    public string? Error { get; set; }
}