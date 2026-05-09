using EBelgeAPI.Data.Interfaces;
using EBelgeAPI.Models.Entities;
using EBelgeAPI.Models.Responses;
using EBelgeAPI.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EBelgeAPI.Controllers;

[ApiController]
[Route("api/cari-filtre")]
[Authorize]
public class CariFiltereController(
    ICariFilterRepository cariFilterRepo,
    ILogService logService) : ControllerBase
{
    private string? Username => User.Identity?.Name;
    private string? Ip => HttpContext.Connection.RemoteIpAddress?.ToString();
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var list = await cariFilterRepo.GetAllAsync();
        return Ok(ApiResponse<List<CariFiltre>>.Ok(list));
    }
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CariFiltre filtre)
    {
        if (string.IsNullOrWhiteSpace(filtre.KimlikNo))
            return BadRequest(ApiResponse<object>.Fail("Kimlik No zorunludur."));
        if (filtre.KimlikTipi != "VKN" && filtre.KimlikTipi != "TCKN")
            return BadRequest(ApiResponse<object>.Fail("Kimlik tipi VKN veya TCKN olmalı."));
        // Sadece rakam kontrolü
        if (!filtre.KimlikNo.All(char.IsDigit))
            return BadRequest(ApiResponse<object>.Fail("Kimlik No sadece rakam içermelidir."));
        // VKN 10 hane
        if (filtre.KimlikTipi == "VKN" && filtre.KimlikNo.Length != 10)
            return BadRequest(ApiResponse<object>.Fail("VKN 10 haneli olmalıdır."));
        // TCKN 11 hane
        if (filtre.KimlikTipi == "TCKN" && filtre.KimlikNo.Length != 11)
            return BadRequest(ApiResponse<object>.Fail("TCKN 11 haneli olmalıdır."));
        // TCKN ilk hane 0 olamaz
        if (filtre.KimlikTipi == "TCKN" && filtre.KimlikNo[0] == '0')
            return BadRequest(ApiResponse<object>.Fail("TCKN 0 ile başlayamaz."));
        // VKN ilk hane 0 olamaz
        if (filtre.KimlikTipi == "VKN" && filtre.KimlikNo[0] == '0')
            return BadRequest(ApiResponse<object>.Fail("VKN 0 ile başlayamaz."));
        // Mükerrer kontrolü
        if (await cariFilterRepo.KimlikNoMevcutMuAsync(filtre.KimlikNo))
            return BadRequest(ApiResponse<object>.Fail(
                $"'{filtre.KimlikNo}' zaten kara listede mevcut."));
        int id = await cariFilterRepo.CreateAsync(filtre);
        filtre.Id = id;
        await logService.InfoAsync(
            $"Cari filtre eklendi: {filtre.KimlikNo} ({filtre.KimlikTipi})",
            username: Username, ip: Ip);
        return Ok(ApiResponse<CariFiltre>.Ok(filtre));
    }
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        await cariFilterRepo.DeleteAsync(id);
        await logService.InfoAsync(
            $"Cari filtre silindi: {id}",
            username: Username, ip: Ip);
        return Ok(ApiResponse<object>.Ok(null));
    }
    [HttpPatch("{id}/active")]
    public async Task<IActionResult> SetActive(int id, [FromQuery] bool isActive)
    {
        await cariFilterRepo.SetActiveAsync(id, isActive);
        return Ok(ApiResponse<object>.Ok(null));
    }
}