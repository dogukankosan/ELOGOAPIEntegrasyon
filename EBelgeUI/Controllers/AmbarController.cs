using EBelgeUI.Filters;
using EBelgeUI.Models;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace EBelgeUI.Controllers;

[SessionAuthFilter]
public class AmbarController(
    IHttpClientFactory httpClientFactory,
    ILogger<AmbarController> logger) : Controller
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
        AmbarIndexViewModel model = new AmbarIndexViewModel();
        try
        {
            HttpClient client = ApiClient();
            HttpResponseMessage resp = await client.GetAsync("/api/ambar");
            string? body = await resp.Content.ReadAsStringAsync();
            AmbarListApiResponse? result = JsonSerializer.Deserialize<AmbarListApiResponse>(body, _jsonOpt);
            if (result?.Success == true)
                model.Ambarlar = result.Data ?? [];
        }
        catch (Exception ex)
        {
            model.ErrorMessage = ex.Message;
        }
        return View(model);
    }
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] AmbarDto ambar)
    {
        try
        {
            HttpClient client = ApiClient();
            string? body = JsonSerializer.Serialize(ambar, _jsonOpt);
            HttpResponseMessage resp = await client.PostAsync("/api/ambar",
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
    public async Task<IActionResult> Update(int id, [FromBody] AmbarDto ambar)
    {
        try
        {
            HttpClient client = ApiClient();
            string? body = JsonSerializer.Serialize(ambar, _jsonOpt);
            HttpResponseMessage resp = await client.PutAsync($"/api/ambar/{id}",
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
            HttpResponseMessage resp = await client.DeleteAsync($"/api/ambar/{id}");
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
                $"/api/ambar/{id}/active?isActive={isActive}", null);
            string? body = await resp.Content.ReadAsStringAsync();
            return Content(body, "application/json");
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }
}