using EBelgeAPI.Data.Interfaces;
using EBelgeAPI.Models.Entities;
using EBelgeAPI.Models.Requests;
using EBelgeAPI.Models.Responses;
using EBelgeAPI.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EBelgeAPI.Controllers;

[ApiController]
[Route("api/ambar")]
[Authorize]
public class AmbarController(
    IAmbarRepository ambarRepo,
    ILogService logService) : ControllerBase
{
    private string? Ip => HttpContext.Connection.RemoteIpAddress?.ToString();
    private string? Username => User.Identity?.Name;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool? isActive = null)
    {
        var list = await ambarRepo.GetAllAsync(isActive);
        return Ok(ApiResponse<List<Ambar>>.Ok(list));
    }
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        Ambar? ambar = await ambarRepo.GetByIdAsync(id);
        if (ambar == null)
            return NotFound(ApiResponse<Ambar>.Fail("Ambar bulunamadı."));
        return Ok(ApiResponse<Ambar>.Ok(ambar));
    }
    [HttpPost("fatura-satis-elemani/map")]
    public async Task<IActionResult> GetFaturaSatisElemaniMap(
    [FromBody] List<string> uuids,
    [FromServices] IFaturaSatisElemaniRepository faturaSatisElemaniRepo)
    {
        if (uuids == null || uuids.Count == 0)
            return Ok(ApiResponse<Dictionary<string, string>>.Ok(new Dictionary<string, string>()));
        var map = await faturaSatisElemaniRepo.GetMapByUuidsAsync(uuids);
        return Ok(ApiResponse<Dictionary<string, string>>.Ok(map));
    }
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Ambar ambar)
    {
        if (string.IsNullOrWhiteSpace(ambar.Kod))
            return BadRequest(ApiResponse<object>.Fail("Ambar kodu zorunludur."));
        if (string.IsNullOrWhiteSpace(ambar.Ad))
            return BadRequest(ApiResponse<object>.Fail("Ambar adı zorunludur."));
        // ── Mükerrer kod kontrolü ──
        if (await ambarRepo.KodMevcutMuAsync(ambar.Kod))
            return BadRequest(ApiResponse<object>.Fail($"'{ambar.Kod}' kodu zaten mevcut."));
        ambar.CreatedAt = DateTime.Now;
        int id = await ambarRepo.CreateAsync(ambar);
        ambar.Id = id;
        await logService.InfoAsync($"Ambar oluşturuldu: {ambar.Kod}", username: Username, ip: Ip);
        return Ok(ApiResponse<Ambar>.Ok(ambar));
    }
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] Ambar ambar,
        [FromServices] IFaturaAmbarRepository faturaAmbarRepo)
    {
        if (string.IsNullOrWhiteSpace(ambar.Ad))
            return BadRequest(ApiResponse<object>.Fail("Ambar adı zorunludur."));
        bool kullaniliyor = await faturaAmbarRepo.IsAmbarUsedAsync(id);
        if (kullaniliyor)
        {
            // Sadece ad değiştirilebilir, kod kontrolü yok
            await ambarRepo.UpdateAdAsync(id, ambar.Ad);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(ambar.Kod))
                return BadRequest(ApiResponse<object>.Fail("Ambar kodu zorunludur."));
            // ── Mükerrer kod kontrolü (kendisi hariç) ──
            if (await ambarRepo.KodMevcutMuAsync(ambar.Kod, excludeId: id))
                return BadRequest(ApiResponse<object>.Fail($"'{ambar.Kod}' kodu zaten mevcut."));
            ambar.Id = id;
            await ambarRepo.UpdateAsync(ambar);
        }
        await logService.InfoAsync($"Ambar güncellendi: {ambar.Kod}", username: Username, ip: Ip);
        return Ok(ApiResponse<object>.Ok(null));
    }
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(
        int id,
        [FromServices] IFaturaAmbarRepository faturaAmbarRepo)
    {
        if (await faturaAmbarRepo.IsAmbarUsedAsync(id))
            return BadRequest(ApiResponse<object>.Fail("Bu ambara bağlı fatura var, silinemez."));
        await ambarRepo.DeleteAsync(id);
        await logService.InfoAsync($"Ambar silindi: {id}", username: Username, ip: Ip);
        return Ok(ApiResponse<object>.Ok(null));
    }
    [HttpPatch("{id}/active")]
    public async Task<IActionResult> SetActive(
        int id,
        [FromQuery] bool isActive,
        [FromServices] IFaturaAmbarRepository faturaAmbarRepo)
    {
        if (!isActive && await faturaAmbarRepo.IsAmbarUsedAsync(id))
            return BadRequest(ApiResponse<object>.Fail("Bu ambara bağlı fatura var, pasife alınamaz."));
        await ambarRepo.SetActiveAsync(id, isActive);
        return Ok(ApiResponse<object>.Ok(null));
    }
    [HttpPost("fatura-ambar")]
    public async Task<IActionResult> SetFaturaAmbar(
    [FromBody] FaturaAmbarRequest request,
    [FromServices] IFaturaAmbarRepository faturaAmbarRepo,
    [FromServices] ITransferRepository transferRepo)
    {
        if (string.IsNullOrWhiteSpace(request.Uuid))
            return BadRequest(ApiResponse<object>.Fail("UUID boş."));
        // Aktarılmış faturada ambar değiştirilemez
        if (await transferRepo.IsTransferredAsync(request.Uuid))
            return BadRequest(ApiResponse<object>.Fail("Aktarılmış faturanın ambarı değiştirilemez."));
        await faturaAmbarRepo.UpsertAsync(request.Uuid, request.AmbarId);
        return Ok(ApiResponse<object>.Ok(null));
    }

    [HttpGet("fatura-ambar/{uuid}")]
    public async Task<IActionResult> GetFaturaAmbar(
        string uuid,
        [FromServices] IFaturaAmbarRepository faturaAmbarRepo)
    {
        var ambarId = await faturaAmbarRepo.GetAmbarIdByUuidAsync(uuid);
        return Ok(ApiResponse<int?>.Ok(ambarId));
    }
    [HttpPost("fatura-ambar/map")]
    public async Task<IActionResult> GetFaturaAmbarMap(
    [FromBody] List<string> uuids,
    [FromServices] IFaturaAmbarRepository faturaAmbarRepo)
    {
        if (uuids == null || uuids.Count == 0)
            return Ok(ApiResponse<Dictionary<string, int>>.Ok(new Dictionary<string, int>()));
        var map = await faturaAmbarRepo.GetAmbarMapByUuidsAsync(uuids);
        return Ok(ApiResponse<Dictionary<string, int>>.Ok(map));
    }
}