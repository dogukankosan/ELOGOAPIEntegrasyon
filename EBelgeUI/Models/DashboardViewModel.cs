namespace EBelgeUI.Models;

// ── ViewModel ────────────────────────────────────────
public class DashboardViewModel
{
    public DashboardStatsDto? Stats { get; set; }
    public List<DashboardTransferDto> SonHataliTransferler { get; set; } = new();
    public List<DashboardTransferDto> SonBasariliTransferler { get; set; } = new();
    public string? ErrorMessage { get; set; }
}
// ── DTOs ─────────────────────────────────────────────
public class DashboardStatsDto
{
    public int ToplamAktarilan { get; set; }
    public int ToplamHatali { get; set; }
    public int ToplamBekleyen { get; set; }
    public int BugunkuAktarilan { get; set; }
    public int BugunkuHatali { get; set; }
    public int Son7GunAktarilan { get; set; }
    public int Son7GunHatali { get; set; }
    public int KaraListeSayisi { get; set; }
    public int AmbarSayisi { get; set; }
}
public class DashboardTransferDto
{
    public string? FaturaNo { get; set; }
    public string? LogoFaturaNo { get; set; }
    public string? Uuid { get; set; }
    public string? HataMesaji { get; set; }
    public DateTime? AktarimTarihi { get; set; }
    public DateTime? CreatedAt { get; set; }
    public int? LogoLogicalRef { get; set; }
}
// ── API Response wrappers ─────────────────────────────
public class DashboardStatsApiResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public DashboardStatsDto? Data { get; set; }
}
public class DashboardTransferApiResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public List<DashboardTransferDto>? Data { get; set; }
}