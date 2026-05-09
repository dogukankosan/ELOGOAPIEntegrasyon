using EBelgeUI.Models;
namespace EBelgeUI.Models;
public class AmbarIndexViewModel
{
    public List<AmbarDto> Ambarlar { get; set; } = new();
    public string? ErrorMessage { get; set; }
}
public class AmbarListApiResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public List<AmbarDto>? Data { get; set; }
}