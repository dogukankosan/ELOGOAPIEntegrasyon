using EBelgeAPI.Models.DTOs;
using EBelgeAPI.Models.Responses;
using EBelgeAPI.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EBelgeAPI.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize]
public class LotTakipController(
    ILotTakipService lotTakipService,
    ILogService logService) : ControllerBase
{
    private string? Username => User.Identity?.Name;
    private string? Ip => HttpContext.Connection.RemoteIpAddress?.ToString();

    [HttpGet("lot-takip")]
    public async Task<IActionResult> GetLotTakip()
    {
        var (success, data, error) = await lotTakipService.GetLotTakipRaporuAsync();
        if (!success)
        {
            await logService.ErrorAsync(error!,
                source: "LotTakip", path: "/api/reports/lot-takip",
                method: "GET", statusCode: 500,
                username: Username, ip: Ip);
            return StatusCode(500, ApiResponse<object>.Fail(error!));
        }
        return Ok(ApiResponse<List<LotTakipDto>>.Ok(data!));
    }
    [HttpPost("lot-takip/cache-sifirla")]
    public async Task<IActionResult> CacheSifirla()
    {
        await lotTakipService.CacheSifirlaAsync();
        await logService.InfoAsync(
            "Lot takip cache sıfırlandı.",
            source: "LotTakip", path: "/api/reports/lot-takip/cache-sifirla",
            method: "POST", statusCode: 200,
            username: Username, ip: Ip);
        return Ok(ApiResponse<object>.Ok(null, "Cache sıfırlandı."));
    }
}