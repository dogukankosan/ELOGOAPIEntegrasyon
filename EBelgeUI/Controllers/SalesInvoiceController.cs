using EBelgeUI.Filters;
using EBelgeUI.Models;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace EBelgeUI.Controllers;

[SessionAuthFilter]
public class SalesInvoiceController(
    IHttpClientFactory httpClientFactory,
    ILogger<SalesInvoiceController> logger) : Controller
{
    private readonly JsonSerializerOptions _jsonOpt = new() { PropertyNameCaseInsensitive = true };
    private readonly ILogger<SalesInvoiceController> _logger = logger;
    private HttpClient ApiClient()
    {
        HttpClient client = httpClientFactory.CreateClient("API");
        string? token = HttpContext.Session.GetString("Token");
        if (!string.IsNullOrEmpty(token))
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
    // ── SATIŞ ELEMANI CACHE YENİLE ────────────────────────
    [HttpPost]
    public async Task<IActionResult> YenileSatisElemaniCache()
    {
        try
        {
            HttpClient client = ApiClient();
            HttpResponseMessage resp = await client.DeleteAsync("/api/satis-elemani/cache");
            string? body = await resp.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<JsonElement>(body, _jsonOpt);
            return Json(result);
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }
    // ── LİSTE ────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Index(SalesInvoiceFilterViewModel filter)
    {
        SalesInvoiceIndexViewModel model = new SalesInvoiceIndexViewModel { Filter = filter };
        // Tarih ikisi de doluysa fatura çek
        bool filterVar = filter.BaslangicTarihi.HasValue && filter.BitisTarihi.HasValue;
        try
        {
            HttpClient client = ApiClient();
            var ambarTask = client.GetAsync("/api/ambar?isActive=true");
            var satisElemaniTask = client.GetAsync("/api/satis-elemani");
            if (filterVar)
            {
                var faturaTask = client.GetAsync($"/api/sales-invoice/list?{BuildQuery(filter)}");
                var transferTask = client.GetAsync("/api/sales-invoice/transferred-uuids");
                await Task.WhenAll(ambarTask, satisElemaniTask, faturaTask, transferTask);
                if (ambarTask.Result.IsSuccessStatusCode)
                {
                    string? body = await ambarTask.Result.Content.ReadAsStringAsync();
                    AmbarApiResponse? result = JsonSerializer.Deserialize<AmbarApiResponse>(body, _jsonOpt);
                    model.Ambarlar = result?.Data ?? [];
                }
                if (satisElemaniTask.Result.IsSuccessStatusCode)
                {
                    string? body = await satisElemaniTask.Result.Content.ReadAsStringAsync();
                    SatisElemaniApiResponse? result = JsonSerializer.Deserialize<SatisElemaniApiResponse>(body, _jsonOpt);
                    model.SatisElemanlari = result?.Data ?? [];
                }
                string? faturaBody = await faturaTask.Result.Content.ReadAsStringAsync();
                SalesInvoiceApiResponse? faturaResult = JsonSerializer.Deserialize<SalesInvoiceApiResponse>(faturaBody, _jsonOpt);
                if (faturaResult?.Success == true)
                {
                    model.Invoices = faturaResult.Data ?? [];
                    model.TotalCount = faturaResult.TotalCount;
                    model.TotalPages = faturaResult.TotalPages;
                    if (transferTask.Result.IsSuccessStatusCode)
                    {
                        string? transferBody = await transferTask.Result.Content.ReadAsStringAsync();
                        SalesInvoiceTransferredResponse? transferResult = JsonSerializer.Deserialize<SalesInvoiceTransferredResponse>(transferBody, _jsonOpt);
                        if (transferResult?.Success == true && transferResult.Data != null)
                            model.AktarilmisUuidler = new HashSet<string>(transferResult.Data);
                    }
                    if (model.Invoices.Any())
                    {
                        var uuids = model.Invoices
                            .Where(i => i.Uuid != null)
                            .Select(i => i.Uuid!)
                            .ToList();
                        string? uuidsJson = JsonSerializer.Serialize(uuids);
                        // Üç map paralel çek
                        var ambarMapTask = client.PostAsync("/api/ambar/fatura-ambar/map",
                            new StringContent(uuidsJson, Encoding.UTF8, "application/json"));
                        var satisMapTask = client.PostAsync("/api/ambar/fatura-satis-elemani/map",
                            new StringContent(uuidsJson, Encoding.UTF8, "application/json"));
                        var hataMapTask = client.PostAsync("/api/sales-invoice/transfer/hata-map",
                            new StringContent(uuidsJson, Encoding.UTF8, "application/json"));
                        await Task.WhenAll(ambarMapTask, satisMapTask, hataMapTask);
                        if (ambarMapTask.Result.IsSuccessStatusCode)
                        {
                            string? mapJson = await ambarMapTask.Result.Content.ReadAsStringAsync();
                            FaturaAmbarMapResponse? mapResult = JsonSerializer.Deserialize<FaturaAmbarMapResponse>(mapJson, _jsonOpt);
                            if (mapResult?.Success == true && mapResult.Data != null)
                                model.FaturaAmbarMap = mapResult.Data;
                        }
                        if (satisMapTask.Result.IsSuccessStatusCode)
                        {
                            string? satisJson = await satisMapTask.Result.Content.ReadAsStringAsync();
                            FaturaSatisElemaniMapResponse? satisResult = JsonSerializer.Deserialize<FaturaSatisElemaniMapResponse>(satisJson, _jsonOpt);
                            if (satisResult?.Success == true && satisResult.Data != null)
                                model.FaturaSatisElemaniMap = satisResult.Data;
                        }
                        if (hataMapTask.Result.IsSuccessStatusCode)
                        {
                            string? hataJson = await hataMapTask.Result.Content.ReadAsStringAsync();
                            HataMapResponse? hataResult = JsonSerializer.Deserialize<HataMapResponse>(hataJson, _jsonOpt);
                            if (hataResult?.Success == true && hataResult.Data != null)
                                model.HataMap = hataResult.Data;
                        }
                    }
                }
                else
                {
                    model.ErrorMessage = faturaResult?.Message ?? "Faturalar alinamadi.";
                }
            }
            else
            {
                await Task.WhenAll(ambarTask, satisElemaniTask);
                if (ambarTask.Result.IsSuccessStatusCode)
                {
                    string? body = await ambarTask.Result.Content.ReadAsStringAsync();
                    AmbarApiResponse? result = JsonSerializer.Deserialize<AmbarApiResponse>(body, _jsonOpt);
                    model.Ambarlar = result?.Data ?? [];
                }

                if (satisElemaniTask.Result.IsSuccessStatusCode)
                {
                    string? body = await satisElemaniTask.Result.Content.ReadAsStringAsync();
                    SatisElemaniApiResponse? result = JsonSerializer.Deserialize<SatisElemaniApiResponse>(body, _jsonOpt);
                    model.SatisElemanlari = result?.Data ?? [];
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SalesInvoice Index hatasi");
            model.ErrorMessage = "API'ye baglanílamiadi.";
        }
       return View(model);
    } 
    // ── TRANSFER ─────────────────────────────────────────
    [HttpPost]
    public async Task<IActionResult> Transfer(string uuid, [FromBody] TransferRequest request)
    {
        if (string.IsNullOrWhiteSpace(uuid))
            return Json(new { success = false, message = "UUID boş." });
        try
        {
            HttpClient client = ApiClient();
            string? body = JsonSerializer.Serialize(request, _jsonOpt);
            HttpResponseMessage resp = await client.PostAsync(
                $"/api/sales-invoice/transfer/{Uri.EscapeDataString(uuid)}",
                new StringContent(body, Encoding.UTF8, "application/json"));
            string? respBody = await resp.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<JsonElement>(respBody, _jsonOpt);
            return Json(result);
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }
    // ── TOPLU TRANSFER ────────────────────────────────────
    [HttpPost]
    public async Task<IActionResult> TopluTransfer([FromBody] TopluTransferRequest request)
    {
        try
        {
            HttpClient client = ApiClient();
            string body = JsonSerializer.Serialize(request, _jsonOpt);
            HttpResponseMessage resp = await client.PostAsync(
                "/api/sales-invoice/transfer/toplu",
                new StringContent(body, Encoding.UTF8, "application/json"));
            string respBody = await resp.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<JsonElement>(respBody, _jsonOpt);
            return Json(result);
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }
    // ── STATÜ BOZ ─────────────────────────────────────────
    [HttpPost]
    public async Task<IActionResult> StatuBoz(string uuid)
    {
        if (string.IsNullOrWhiteSpace(uuid))
            return Json(new { success = false, message = "UUID boş." });
        try
        {
            HttpClient client = ApiClient();
            HttpResponseMessage resp = await client.PostAsync(
                $"/api/sales-invoice/statu-boz/{Uri.EscapeDataString(uuid)}", null);
            string? body = await resp.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<JsonElement>(body, _jsonOpt);
            return Json(result);
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }
    // ── TOPLU STATÜ BOZ ───────────────────────────────────
    [HttpPost]
    public async Task<IActionResult> TopluStatuBoz([FromBody] List<string> uuids)
    {
        try
        {
            HttpClient client = ApiClient();
            string? body = JsonSerializer.Serialize(uuids, _jsonOpt);
            HttpResponseMessage resp = await client.PostAsync(
                "/api/sales-invoice/toplu-statu-boz",
                new StringContent(body, Encoding.UTF8, "application/json"));
            string? respBody = await resp.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<JsonElement>(respBody, _jsonOpt);
            return Json(result);
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }
    // ── FATURA AMBAR KAYDET ───────────────────────────────
    [HttpPost]
    public async Task<IActionResult> SetFaturaAmbar(string uuid, int ambarId)
    {
        try
        {
            HttpClient client = ApiClient();
            string? body = JsonSerializer.Serialize(new { uuid, ambarId }, _jsonOpt);
            HttpResponseMessage resp = await client.PostAsync(
                "/api/ambar/fatura-ambar",
                new StringContent(body, Encoding.UTF8, "application/json"));
            string? respBody = await resp.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<JsonElement>(respBody, _jsonOpt);
            return Json(result);
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }
    // ── GÖRSEL ───────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Preview(
        string uuid,
        string format = "html",
        string docType = "einvoice")   // ← eklendi
    {
        if (string.IsNullOrWhiteSpace(uuid))
            return Json(new { success = false, message = "UUID boş." });
        try
        {
            HttpClient client = ApiClient();
            HttpResponseMessage resp = await client.GetAsync(
                $"/api/sales-invoice/visual/{Uri.EscapeDataString(uuid)}" +
                $"?format={format}&docType={docType}");  // ← docType geçiliyor
            if (!resp.IsSuccessStatusCode)
                return Json(new { success = false, message = "Görsel alınamadı." });
            if (format.Equals("pdf", StringComparison.OrdinalIgnoreCase))
            {
                byte[] bytes = await resp.Content.ReadAsByteArrayAsync();
                return File(bytes, "application/pdf");
            }
            string? html = await resp.Content.ReadAsStringAsync();
            return Json(new { success = true, html });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }
    // ── DETAY ─────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Detail(
        string uuid,
        string docType = "einvoice")   // ← eklendi
    {
        if (string.IsNullOrWhiteSpace(uuid))
            return Json(new { success = false, message = "UUID boş." });
        try
        {
            HttpClient client = ApiClient();
            HttpResponseMessage resp = await client.GetAsync(
                $"/api/sales-invoice/detail/{Uri.EscapeDataString(uuid)}" +
                $"?docType={docType}");   // ← docType geçiliyor
            string? body = await resp.Content.ReadAsStringAsync();
            SalesInvoiceDetailApiResponse? result = JsonSerializer.Deserialize<SalesInvoiceDetailApiResponse>(body, _jsonOpt);
            if (result?.Success == true)
                return Json(new { success = true, data = result.Data });

            return Json(new { success = false, message = result?.Message ?? "Detay alınamadı." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }
    // ── HELPERS ───────────────────────────────────────────
    private static string BuildQuery(SalesInvoiceFilterViewModel f)
    {
        List<string> q = new List<string>();
        if (f.BaslangicTarihi.HasValue)
            q.Add($"BaslangicTarihi={f.BaslangicTarihi.Value:yyyy-MM-dd}");
        if (f.BitisTarihi.HasValue)
            q.Add($"BitisTarihi={f.BitisTarihi.Value:yyyy-MM-dd}");
        if (!string.IsNullOrWhiteSpace(f.FaturaNo))
            q.Add($"FaturaNo={Uri.EscapeDataString(f.FaturaNo)}");
        if (!string.IsNullOrWhiteSpace(f.AliciVkn))
            q.Add($"AliciVkn={Uri.EscapeDataString(f.AliciVkn)}");
        if (!string.IsNullOrWhiteSpace(f.AliciUnvan))
            q.Add($"AliciUnvan={Uri.EscapeDataString(f.AliciUnvan)}");
        q.Add($"Page={f.Page}");
        q.Add($"PageSize={f.PageSize}");
        return string.Join("&", q);
    }
}