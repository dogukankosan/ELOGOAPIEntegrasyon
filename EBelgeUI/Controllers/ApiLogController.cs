using EBelgeUI.Filters;
using EBelgeUI.Models;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Text.Json;

namespace EBelgeUI.Controllers;

[SessionAuthFilter]
public class ApiLogController(
    IHttpClientFactory httpClientFactory,
    ILogger<ApiLogController> logger) : Controller
{
    private readonly JsonSerializerOptions _jsonOpt = new() { PropertyNameCaseInsensitive = true };
    private HttpClient ApiClient()
    {
        HttpClient client = httpClientFactory.CreateClient("API");
        string? token = HttpContext.Session.GetString("Token");
        if (!string.IsNullOrEmpty(token))
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
    [HttpGet]
    public async Task<IActionResult> Index(ApiLogFilterViewModel filter)
    {
        ApiLogIndexViewModel model = new ApiLogIndexViewModel { Filter = filter };
        try
        {
            HttpClient client = ApiClient();
            var sourcesTask = client.GetAsync("/api/log/sources");
            var usernamesTask = client.GetAsync("/api/log/usernames");
            var logsTask = client.GetAsync($"/api/log?{BuildQuery(filter)}");
            await Task.WhenAll(sourcesTask, usernamesTask, logsTask);
            if (sourcesTask.Result.IsSuccessStatusCode)
            {
                string? body = await sourcesTask.Result.Content.ReadAsStringAsync();
                ApiLogSourceResponse? result = JsonSerializer.Deserialize<ApiLogSourceResponse>(body, _jsonOpt);
                model.Sources = result?.Data ?? [];
            }
            if (usernamesTask.Result.IsSuccessStatusCode)
            {
                string? body = await usernamesTask.Result.Content.ReadAsStringAsync();
                ApiLogSourceResponse? result = JsonSerializer.Deserialize<ApiLogSourceResponse>(body, _jsonOpt);
                model.Usernames = result?.Data ?? [];
            }
            string? logsBody = await logsTask.Result.Content.ReadAsStringAsync();
            ApiLogApiResponse? logsResult = JsonSerializer.Deserialize<ApiLogApiResponse>(logsBody, _jsonOpt);
            if (logsResult?.Success == true)
            {
                model.Logs = logsResult.Data ?? [];
                model.TotalCount = logsResult.TotalCount;
                model.TotalPages = logsResult.TotalPages;
            }
            else
            {
                model.ErrorMessage = logsResult?.Message ?? "Loglar alınamadı.";
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "ApiLog Index hatası");
            model.ErrorMessage = "API'ye bağlanılamadı.";
        }
        return View(model);
    }
    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            HttpClient client = ApiClient();
            HttpResponseMessage resp = await client.DeleteAsync($"/api/log/{id}");
            string? body = await resp.Content.ReadAsStringAsync();
            return Content(body, "application/json");
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }
    [HttpPost]
    public async Task<IActionResult> DeleteAll(string? level = null, string? source = null)
    {
        try
        {
            HttpClient client = ApiClient();
            List<string> query = new List<string>();
            if (!string.IsNullOrWhiteSpace(level)) query.Add($"level={Uri.EscapeDataString(level)}");
            if (!string.IsNullOrWhiteSpace(source)) query.Add($"source={Uri.EscapeDataString(source)}");
            string? qs = query.Count > 0 ? "?" + string.Join("&", query) : "";
            HttpResponseMessage resp = await client.DeleteAsync($"/api/log/all{qs}");
            string? body = await resp.Content.ReadAsStringAsync();
            return Content(body, "application/json");
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }
    [HttpGet]
    public async Task<IActionResult> Export(ApiLogFilterViewModel filter)
    {
        try
        {
            filter.PageSize = 10000;
            HttpClient client = ApiClient();
            HttpResponseMessage resp = await client.GetAsync($"/api/log/export?{BuildQuery(filter)}");
            byte[] bytes = await resp.Content.ReadAsByteArrayAsync();
            return File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"api-logs-{DateTime.Now:yyyyMMdd-HHmm}.xlsx");
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }
    private static string BuildQuery(ApiLogFilterViewModel f)
    {
        List<string> q = new List<string>();
        if (!string.IsNullOrWhiteSpace(f.Level)) q.Add($"Level={Uri.EscapeDataString(f.Level)}");
        if (!string.IsNullOrWhiteSpace(f.Source)) q.Add($"Source={Uri.EscapeDataString(f.Source)}");
        if (!string.IsNullOrWhiteSpace(f.Username)) q.Add($"Username={Uri.EscapeDataString(f.Username)}");
        if (!string.IsNullOrWhiteSpace(f.Path)) q.Add($"Path={Uri.EscapeDataString(f.Path)}");
        if (f.StatusCode.HasValue) q.Add($"StatusCode={f.StatusCode}");
        if (f.BaslangicTarihi.HasValue) q.Add($"BaslangicTarihi={f.BaslangicTarihi.Value:yyyy-MM-dd}");
        if (f.BitisTarihi.HasValue) q.Add($"BitisTarihi={f.BitisTarihi.Value:yyyy-MM-dd}");
        q.Add($"Page={f.Page}");
        q.Add($"PageSize={f.PageSize}");
        return string.Join("&", q);
    }
}