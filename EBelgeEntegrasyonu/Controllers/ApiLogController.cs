using ClosedXML.Excel;
using EBelgeAPI.Data.Interfaces;
using EBelgeAPI.Models.Entities;
using EBelgeAPI.Models.Requests;
using EBelgeAPI.Models.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace EBelgeAPI.Controllers;

[ApiController]
[Route("api/log")]
[Authorize]
public class ApiLogController(IApiLogRepository logRepo) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] ApiLogFilterRequest filter)
    {
        if (filter.Page < 1) filter.Page = 1;
        if (filter.PageSize < 1) filter.PageSize = 50;
        if (filter.PageSize > 200) filter.PageSize = 200;
        var (data, total) = await logRepo.GetListAsync(filter);
        return Ok(PagedResponse<ApiLog>.Ok(data, total, filter.Page, filter.PageSize));
    }
  
    [HttpGet("sources")]
    public async Task<IActionResult> GetSources()
    {
        var sources = await logRepo.GetDistinctSourcesAsync();
        return Ok(ApiResponse<List<string>>.Ok(sources));
    }
    [HttpGet("usernames")]
    public async Task<IActionResult> GetUsernames()
    {
        var usernames = await logRepo.GetDistinctUsernamesAsync();
        return Ok(ApiResponse<List<string>>.Ok(usernames));
    }
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        await logRepo.DeleteAsync(id);
        return Ok(ApiResponse<object>.Ok(null));
    }
    [HttpDelete("all")]
    public async Task<IActionResult> DeleteAll([FromQuery] ApiLogFilterRequest? filter = null)
    {
        await logRepo.DeleteAllAsync(filter);
        return Ok(ApiResponse<object>.Ok(null));
    }
    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] ApiLogFilterRequest filter)
    {
        filter.Page = 1;
        filter.PageSize = 10000;
        var (data, _) = await logRepo.GetListAsync(filter);
        using XLWorkbook workbook = new ClosedXML.Excel.XLWorkbook();
        var ws = workbook.Worksheets.Add("API Logları");
        // Başlıklar
        string[] headers = new[] { "Id", "Level", "Source", "Method", "Path", "StatusCode", "Message", "Username", "IpAddress", "Duration(ms)", "Tarih" };
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml("#1a1a2e");
            cell.Style.Font.FontColor = ClosedXML.Excel.XLColor.White;
            cell.Style.Alignment.Horizontal = ClosedXML.Excel.XLAlignmentHorizontalValues.Center;
        }
        // Veri
        for (int i = 0; i < data.Count; i++)
        {
            ApiLog log = data[i];
            int row = i + 2;
            ws.Cell(row, 1).Value = log.Id;
            ws.Cell(row, 2).Value = log.Level;
            ws.Cell(row, 3).Value = log.Source;
            ws.Cell(row, 4).Value = log.Method;
            ws.Cell(row, 5).Value = log.Path;
            ws.Cell(row, 6).Value = log.StatusCode;
            ws.Cell(row, 7).Value = log.Message;
            ws.Cell(row, 8).Value = log.Username;
            ws.Cell(row, 9).Value = log.IpAddress;
            ws.Cell(row, 10).Value = log.Duration;
            ws.Cell(row, 11).Value = log.CreatedAt.ToString("dd.MM.yyyy HH:mm:ss");
            // Level'e göre renk
            XLColor levelColor = log.Level?.ToUpper() switch
            {
                "ERROR" => ClosedXML.Excel.XLColor.FromHtml("#fce8e6"),
                "WARNING" or "WARN" => ClosedXML.Excel.XLColor.FromHtml("#fff3e0"),
                _ => ClosedXML.Excel.XLColor.NoColor
            };
            if (levelColor != ClosedXML.Excel.XLColor.NoColor)
                ws.Row(row).Style.Fill.BackgroundColor = levelColor;
        }
        // Kolon genişlikleri
        ws.Columns().AdjustToContents();
        ws.Column(7).Width = 60; // Message kolonu sabit
        // Filtrele dondur
        ws.SheetView.FreezeRows(1);
        ws.RangeUsed()?.SetAutoFilter();
        using MemoryStream stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Seek(0, SeekOrigin.Begin);
        return File(stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"api-logs-{DateTime.Now:yyyyMMdd-HHmm}.xlsx");
    }
    [HttpPost("write")]
    [AllowAnonymous] // UI'dan token olmadan da atabilsin
    public async Task<IActionResult> Write([FromBody] ApiLog log)
    {
        log.CreatedAt = DateTime.Now;
        await logRepo.WriteAsync(log);
        return Ok(ApiResponse<object>.Ok(null));
    }
}