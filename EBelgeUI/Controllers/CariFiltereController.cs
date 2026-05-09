using EBelgeUI.Filters;
using EBelgeUI.Models;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace EBelgeUI.Controllers;

[SessionAuthFilter]
public class CariFiltereController(
    IHttpClientFactory httpClientFactory,
    ILogger<CariFiltereController> logger) : Controller
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
    public async Task<IActionResult> Index()
    {
        CariFiltereIndexViewModel model = new CariFiltereIndexViewModel();
        try
        {
            HttpClient client = ApiClient();
            HttpResponseMessage resp = await client.GetAsync("/api/cari-filtre");
            string? body = await resp.Content.ReadAsStringAsync();
            CariFiltereApiResponse? result = JsonSerializer.Deserialize<CariFiltereApiResponse>(body, _jsonOpt);
            if (result?.Success == true)
                model.Filtreler = result.Data ?? [];
            else
                model.ErrorMessage = result?.Message ?? "Veriler alınamadı.";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "CariFiltere Index hatası");
            model.ErrorMessage = "API'ye bağlanılamadı.";
        }
        return View(model);
    }
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CariFiltereDto filtre)
    {
        try
        {
            HttpClient client = ApiClient();
            string? body = JsonSerializer.Serialize(filtre, _jsonOpt);
            HttpResponseMessage resp = await client.PostAsync("/api/cari-filtre",
                new StringContent(body, Encoding.UTF8, "application/json"));
            string? respBody = await resp.Content.ReadAsStringAsync();
            return Content(respBody, "application/json");
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }
    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            HttpClient client = ApiClient();
            HttpResponseMessage resp = await client.DeleteAsync($"/api/cari-filtre/{id}");
            string? body = await resp.Content.ReadAsStringAsync();
            return Content(body, "application/json");
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }
    [HttpPost]
    public async Task<IActionResult> SetActive(int id, bool isActive)
    {
        try
        {
            HttpClient client = ApiClient();
            HttpResponseMessage resp = await client.PatchAsync(
                $"/api/cari-filtre/{id}/active?isActive={isActive}", null);
            string? body = await resp.Content.ReadAsStringAsync();
            return Content(body, "application/json");
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }
}