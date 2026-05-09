namespace EBelgeAPI.Models.Requests;
public class TransferRequest
{
    public string AmbarKodu { get; set; } = "";
    public string SatisElemaniKodu { get; set; } = "";
    public string DocType { get; set; } = "einvoice";  // "einvoice" | "earchive"
}
public class TopluTransferItem
{
    public string Uuid { get; set; } = "";
    public string DocType { get; set; } = "einvoice";
    public string AmbarKodu { get; set; } = "";
    public string SatisElemaniKodu { get; set; } = "";
}
// YENİ:
public class TopluTransferRequest
{
    public List<TopluTransferItem> Items { get; set; } = new();
}