namespace EBelgeAPI.Models.Requests;

public class InvoiceVisualRequest
{
    public string Uuid { get; set; } = "";
    public VisualFormat Format { get; set; } = VisualFormat.Html;
}
public enum VisualFormat
{
    Html = 0,
    Pdf = 1
}