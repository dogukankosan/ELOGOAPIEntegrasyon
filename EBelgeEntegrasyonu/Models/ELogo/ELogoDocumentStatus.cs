namespace EBelgeAPI.Models.ELogo;

public class ELogoDocumentStatus
{
    public int Status { get; set; }
    public int Code { get; set; }
    public string? Description { get; set; }
    public string? EnvelopeId { get; set; }
    public bool IsCancel { get; set; }
}