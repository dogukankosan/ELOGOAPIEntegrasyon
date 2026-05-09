using EBelgeAPI.Data.Interfaces;
using EBelgeAPI.Models.DTOs;
using EBelgeAPI.Models.ELogo;
using EBelgeAPI.Models.Entities;
using EBelgeAPI.Models.Requests;
using EBelgeAPI.Models.Responses;
using EBelgeAPI.Services;
using EBelgeAPI.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace EBelgeAPI.Controllers;

[ApiController]
[Route("api/sales-invoice")]
[Authorize]
public class SalesInvoiceController(
    ISalesInvoiceService salesInvoiceService,
    ILogService logService) : ControllerBase
{
    private string? Username => User.Identity?.Name;
    private string? Ip => HttpContext.Connection.RemoteIpAddress?.ToString();

    // ── DocType string → enum ─────────────────────────────
    private static ELogoDocumentType ParseDocType(string? docType) =>
        docType?.Equals("earchive", StringComparison.OrdinalIgnoreCase) == true
            ? ELogoDocumentType.EArchive
            : ELogoDocumentType.EInvoice;
    // ── UBL RAW ───────────────────────────────────────────
    [HttpGet("ubl-raw/{uuid}")]
    public async Task<IActionResult> GetUblRaw(
        string uuid,
        [FromQuery] string docType = "einvoice")
    {
        if (string.IsNullOrWhiteSpace(uuid))
            return BadRequest(ApiResponse<object>.Fail("UUID zorunludur."));
        var (ok, xml, err) = await salesInvoiceService
            .GetSalesInvoiceUblRawAsync(uuid, ParseDocType(docType));
        if (!ok)
            return BadRequest(ApiResponse<object>.Fail(err!));
        return Content(xml!, "application/xml");
    }
    [HttpPost("transfer/hata-map")]
    public async Task<IActionResult> GetHataMap(
    [FromBody] List<string> uuids,
    [FromServices] ITransferRepository transferRepo)
    {
        if (uuids == null || uuids.Count == 0)
            return Ok(ApiResponse<Dictionary<string, string>>.Ok(new()));
        var map = await transferRepo.GetHataMapByUuidsAsync(uuids);
        return Ok(ApiResponse<Dictionary<string, string>>.Ok(map));
    }
    // ── AKTARILMIŞ UUID LİSTESİ ───────────────────────────
    [HttpGet("transferred-uuids")]
    public async Task<IActionResult> GetTransferredUuids(
        [FromServices] ITransferRepository transferRepo)
    {
        var uuids = await transferRepo.GetTransferredUuidsAsync();
        return Ok(ApiResponse<List<string>>.Ok(uuids));
    }
    // ── TEK TRANSFER ──────────────────────────────────────
    [HttpPost("transfer/{uuid}")]
    public async Task<IActionResult> Transfer(
        string uuid,
        [FromBody] TransferRequest request,
        [FromServices] ILogoTransferService transferService,
        [FromServices] IFaturaAmbarRepository faturaAmbarRepo,
        [FromServices] IAmbarRepository ambarRepo,
        [FromServices] IFaturaSatisElemaniRepository faturaSatisElemaniRepo)
    {
        if (string.IsNullOrWhiteSpace(request.AmbarKodu))
            return BadRequest(ApiResponse<object>.Fail("Ambar kodu zorunludur."));
        if (string.IsNullOrWhiteSpace(request.SatisElemaniKodu))
            return BadRequest(ApiResponse<object>.Fail("Satış elemanı kodu zorunludur."));
        Ambar? ambar = await ambarRepo.GetByKodAsync(request.AmbarKodu);
        if (ambar == null)
            return BadRequest(ApiResponse<object>.Fail("Ambar bulunamadı."));
        // ← docType artık request'ten geliyor
        var elogoDocType = ParseDocType(request.DocType);
        var (detailOk, dto, detailErr) =
            await salesInvoiceService.GetSalesInvoiceDetailAsync(uuid, elogoDocType);
        if (!detailOk || dto == null)
           return BadRequest(ApiResponse<object>.Fail(detailErr!));
        await faturaAmbarRepo.UpsertAsync(uuid, ambar.Id);
        await faturaSatisElemaniRepo.UpsertAsync(uuid, request.SatisElemaniKodu);
        var (ok, result, err) = await transferService.TransferAsync(
            dto, request.AmbarKodu, request.SatisElemaniKodu);
        if (!ok)
        {
            await logService.ErrorAsync(
                $"Transfer başarısız. UUID: {uuid}",
                source: "Transfer",
                path: $"/api/sales-invoice/transfer/{uuid}",
                method: "POST", statusCode: 400,
                username: Username, ip: Ip, detail: err);
            return BadRequest(ApiResponse<object>.Fail(err!));
        }
        await logService.InfoAsync(
            $"Transfer başarılı. UUID: {uuid} → Logo: {result!.LogoFaturaNo}",
            source: "Transfer",
            path: $"/api/sales-invoice/transfer/{uuid}",
            method: "POST", statusCode: 200,
            username: Username, ip: Ip);
        return Ok(ApiResponse<SalesTransferResultDto>.Ok(result!));
    }
    // ── TOPLU TRANSFER ────────────────────────────────────
    [HttpPost("transfer/toplu")]
    public async Task<IActionResult> TopluTransfer(
        [FromBody] TopluTransferRequest request,
        [FromServices] ILogoTransferService transferService,
        [FromServices] IFaturaAmbarRepository faturaAmbarRepo,
        [FromServices] IAmbarRepository ambarRepo,
        [FromServices] IFaturaSatisElemaniRepository faturaSatisElemaniRepo)
    {
        if (request.Items == null || request.Items.Count == 0)
            return BadRequest(ApiResponse<object>.Fail("UUID listesi boş."));
        if (request.Items.Count > 50)
            return BadRequest(ApiResponse<object>.Fail(
                "Tek seferde en fazla 50 fatura gönderilebilir."));
        List<SalesTransferResultDto> results = new List<SalesTransferResultDto>();
        foreach (var item in request.Items)
        {
            if (string.IsNullOrWhiteSpace(item.AmbarKodu))
            {
                results.Add(new SalesTransferResultDto
                { Uuid = item.Uuid, Success = false, Error = "Ambar kodu zorunludur." });
                continue;
            }
            if (string.IsNullOrWhiteSpace(item.SatisElemaniKodu))
            {
                results.Add(new SalesTransferResultDto
                { Uuid = item.Uuid, Success = false, Error = "Satış elemanı kodu zorunludur." });
                continue;
            }
            Ambar? ambar = await ambarRepo.GetByKodAsync(item.AmbarKodu);
            if (ambar == null)
            {
                results.Add(new SalesTransferResultDto
                { Uuid = item.Uuid, Success = false, Error = $"Ambar bulunamadı: {item.AmbarKodu}" });
                continue;
            }
            var elogoDocType = ParseDocType(item.DocType);
            var (detailOk, dto, detailErr) =
                await salesInvoiceService.GetSalesInvoiceDetailAsync(item.Uuid, elogoDocType);
            if (!detailOk || dto == null)
            {
                results.Add(new SalesTransferResultDto
                { Uuid = item.Uuid, Success = false, Error = detailErr ?? "Fatura detayı alınamadı." });
                continue;
            }
            await faturaAmbarRepo.UpsertAsync(item.Uuid, ambar.Id);
            await faturaSatisElemaniRepo.UpsertAsync(item.Uuid, item.SatisElemaniKodu);
            var (ok, result, err) = await transferService.TransferAsync(
                dto, item.AmbarKodu, item.SatisElemaniKodu);
            results.Add(result ?? new SalesTransferResultDto
            {
                Uuid = item.Uuid,
                ELogoFaturaNo = dto.FaturaNo,
                Success = false,
                Error = err
            });
        }
        int basarili = results.Count(r => r.Success);
        int basarisiz = results.Count(r => !r.Success);
        await logService.InfoAsync(
            $"Toplu transfer. Başarılı: {basarili}, Başarısız: {basarisiz}",
            source: "Transfer", path: "/api/sales-invoice/transfer/toplu",
            method: "POST", statusCode: 200, username: Username, ip: Ip);
        return Ok(ApiResponse<List<SalesTransferResultDto>>.Ok(results));
    }
    // ── STATÜ BOZ ─────────────────────────────────────────
    [HttpPost("statu-boz/{uuid}")]
    public async Task<IActionResult> StatuBoz(
        string uuid,
        [FromServices] ITransferRepository transferRepo,
        [FromServices] ILogoTransferService transferService)
    {
        ELogoLogoTransfer? transfer = await transferRepo.GetByUuidAsync(uuid);
        if (transfer == null)
            return BadRequest(ApiResponse<object>.Fail("Transfer kaydı bulunamadı."));
        if (transfer.AktarimDurumu != 1)
            return BadRequest(ApiResponse<object>.Fail("Bu fatura aktarılmış değil."));
        if (!string.IsNullOrEmpty(transfer.LogoFaturaNo))
        {
            bool logodaVar = await transferService.FaturaLogodaVarMiAsync(transfer.LogoFaturaNo);
            if (logodaVar)
                return BadRequest(ApiResponse<object>.Fail(
                    $"Bu fatura Logo ERP'de mevcut (No: {transfer.LogoFaturaNo}). " +
                    "Önce Logo'dan silinmesi gerekiyor."));
        }
        transfer.AktarimDurumu = 0;
        transfer.LogoFaturaNo = null;
        transfer.LogoLogicalRef = null;
        transfer.HataMesaji = "Manuel statü bozma yapıldı.";
        transfer.AktarimTarihi = null;
        await transferRepo.UpdateAsync(transfer);
        await logService.InfoAsync(
            $"Transfer statüsü bozuldu. UUID: {uuid}",
            source: "StatuBoz", username: Username, ip: Ip);
        return Ok(ApiResponse<object>.Ok(null));
    }
    // ── TOPLU STATÜ BOZ ───────────────────────────────────
    [HttpPost("toplu-statu-boz")]
    public async Task<IActionResult> TopluStatuBoz(
        [FromBody] List<string> uuids,
        [FromServices] ITransferRepository transferRepo,
        [FromServices] ILogoTransferService transferService)
    {
        if (uuids == null || uuids.Count == 0)
            return BadRequest(ApiResponse<object>.Fail("UUID listesi boş."));
        int basarili = 0, basarisiz = 0;
        List<string> hatalar = new List<string>();
        foreach (string uuid in uuids)
        {
            ELogoLogoTransfer? transfer = await transferRepo.GetByUuidAsync(uuid);
            if (transfer == null || transfer.AktarimDurumu != 1)
            {
                basarisiz++;
                continue;
            }
            if (!string.IsNullOrEmpty(transfer.LogoFaturaNo))
            {
                var logodaVar = await transferService.FaturaLogodaVarMiAsync(transfer.LogoFaturaNo);
                if (logodaVar)
                {
                    basarisiz++;
                    hatalar.Add($"{transfer.LogoFaturaNo} Logo'da mevcut, önce silin.");
                    continue;
                }
            }
            transfer.AktarimDurumu = 0;
            transfer.LogoFaturaNo = null;
            transfer.LogoLogicalRef = null;
            transfer.HataMesaji = "Toplu statü bozma yapıldı.";
            transfer.AktarimTarihi = null;
            await transferRepo.UpdateAsync(transfer);
            basarili++;
        }
        await logService.InfoAsync(
            $"Toplu statü bozma. Başarılı: {basarili}, Başarısız: {basarisiz}",
            source: "StatuBoz", username: Username, ip: Ip);
        return Ok(ApiResponse<object>.Ok(new { basarili, basarisiz, hatalar }));
    }
    // ── LİSTE ─────────────────────────────────────────────
    [HttpGet("list")]
    public async Task<IActionResult> GetList([FromQuery] SalesInvoiceListRequest request)
    {
        if (request.Page < 1) request.Page = 1;
        if (request.PageSize < 1) request.PageSize = 50;
        if (request.PageSize > 500) request.PageSize = 500;
        Stopwatch sw = Stopwatch.StartNew();
        try
        {
            var (success, data, totalCount, error) =
                await salesInvoiceService.GetSalesInvoicesAsync(request);
            sw.Stop();
            if (!success)
            {
                await logService.ErrorAsync(error!,
                    path: "/api/sales-invoice/list", method: "GET",
                    statusCode: 400, username: Username, ip: Ip,
                    duration: (int)sw.ElapsedMilliseconds);
                return BadRequest(PagedResponse<SalesInvoiceDto>.Fail(error!));
            }
            await logService.InfoAsync($"{totalCount} fatura listelendi.",
                path: "/api/sales-invoice/list", method: "GET",
                statusCode: 200, username: Username, ip: Ip,
                duration: (int)sw.ElapsedMilliseconds);

            return Ok(PagedResponse<SalesInvoiceDto>.Ok(
                data!, totalCount, request.Page, request.PageSize));
        }
        catch (Exception ex)
        {
            sw.Stop();
            await logService.ErrorAsync(ex.Message,
                path: "/api/sales-invoice/list", method: "GET",
                statusCode: 500, username: Username, ip: Ip,
                duration: (int)sw.ElapsedMilliseconds);
            return StatusCode(500, PagedResponse<SalesInvoiceDto>.Fail("Sunucu hatası oluştu."));
        }
    }
    // ── GÖRSEL ────────────────────────────────────────────
    [HttpGet("visual/{uuid}")]
    public async Task<IActionResult> GetVisual(
        string uuid,
        [FromQuery] string format = "html",
        [FromQuery] string docType = "einvoice")
    {
        if (string.IsNullOrWhiteSpace(uuid))
            return BadRequest(ApiResponse<object>.Fail("UUID zorunludur."));
        VisualFormat visualFormat = format.Equals("pdf", StringComparison.OrdinalIgnoreCase)
            ? VisualFormat.Pdf : VisualFormat.Html;
        Stopwatch sw = Stopwatch.StartNew();
        try
        {
            var (success, data, contentType, error) =
                await salesInvoiceService.GetSalesInvoiceVisualAsync(
                    uuid, visualFormat, ParseDocType(docType));
            sw.Stop();
            if (!success)
            {
                await logService.ErrorAsync(error!,
                    path: $"/api/sales-invoice/visual/{uuid}", method: "GET",
                    statusCode: 400, username: Username, ip: Ip,
                    duration: (int)sw.ElapsedMilliseconds);
                return BadRequest(ApiResponse<object>.Fail(error!));
            }
            if (visualFormat == VisualFormat.Pdf)
            {
                Response.Headers.Append("Content-Disposition",
                    $"inline; filename={uuid}.pdf");
                return File(data!, contentType);
            }
            return Content(System.Text.Encoding.UTF8.GetString(data!), contentType);
        }
        catch (Exception ex)
        {
            sw.Stop();
            await logService.ErrorAsync(ex.Message,
                path: $"/api/sales-invoice/visual/{uuid}", method: "GET",
                statusCode: 500, username: Username, ip: Ip,
                duration: (int)sw.ElapsedMilliseconds);
            return StatusCode(500, ApiResponse<object>.Fail("Sunucu hatası oluştu."));
        }
    }
    // ── DETAY ─────────────────────────────────────────────
    [HttpGet("detail/{uuid}")]
    public async Task<IActionResult> GetDetail(
        string uuid,
        [FromQuery] string docType = "einvoice")
    {
        if (string.IsNullOrWhiteSpace(uuid))
            return BadRequest(ApiResponse<SalesInvoiceDto>.Fail("UUID zorunludur."));
        Stopwatch sw = Stopwatch.StartNew();
        try
        {
            var (success, data, error) =
                await salesInvoiceService.GetSalesInvoiceDetailAsync(
                    uuid, ParseDocType(docType));
            sw.Stop();
            if (!success)
            {
                await logService.ErrorAsync(error!,
                    path: $"/api/sales-invoice/detail/{uuid}", method: "GET",
                    statusCode: 400, username: Username, ip: Ip,
                    duration: (int)sw.ElapsedMilliseconds);
                return BadRequest(ApiResponse<SalesInvoiceDto>.Fail(error!));
            }
            return Ok(ApiResponse<SalesInvoiceDto>.Ok(data!));
        }
        catch (Exception ex)
        {
            sw.Stop();
            await logService.ErrorAsync(ex.Message,
                path: $"/api/sales-invoice/detail/{uuid}", method: "GET",
                statusCode: 500, username: Username, ip: Ip,
                duration: (int)sw.ElapsedMilliseconds);
            return StatusCode(500, ApiResponse<SalesInvoiceDto>.Fail("Sunucu hatası oluştu."));
        }
    }
}