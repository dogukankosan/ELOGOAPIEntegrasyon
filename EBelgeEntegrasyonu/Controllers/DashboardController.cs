using EBelgeAPI.Data.Interfaces;
using EBelgeAPI.Models.DTOs;
using EBelgeAPI.Models.Responses;
using Microsoft.AspNetCore.Mvc;

namespace EBelgeAPI.Controllers;

[ApiController]
[Route("api/dashboard")]
public class DashboardController : ControllerBase
{
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(
        [FromServices] IDashboardRepository repo)
    {
        DashboardStatsDto? stats = await repo.GetStatsAsync();
        return Ok(ApiResponse<object>.Ok(new
        {
            stats.ToplamAktarilan,
            stats.ToplamHatali,
            stats.ToplamBekleyen,
            stats.BugunkuAktarilan,
            stats.BugunkuHatali,
            stats.Son7GunAktarilan,
            stats.Son7GunHatali,
            stats.KaraListeSayisi,
            stats.AmbarSayisi
        }));
    }

    [HttpGet("son-hatali")]
    public async Task<IActionResult> GetSonHatali(
        [FromServices] IDashboardRepository repo,
        [FromQuery] int limit = 5)
    {
        var list = await repo.GetSonHataliTransferlerAsync(limit);
        return Ok(ApiResponse<object>.Ok(list));
    }

    [HttpGet("son-basarili")]
    public async Task<IActionResult> GetSonBasarili(
        [FromServices] IDashboardRepository repo,
        [FromQuery] int limit = 5)
    {
        var list = await repo.GetSonBasariliTransferlerAsync(limit);
        return Ok(ApiResponse<object>.Ok(list));
    }
}