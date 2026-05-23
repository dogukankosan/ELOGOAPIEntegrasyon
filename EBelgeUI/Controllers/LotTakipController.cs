// LotTakipController.cs (UI)
using EBelgeUI.Filters;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Text.Json;

[SessionAuthFilter]
public class LotTakipController(
    IHttpClientFactory httpClientFactory,
    ILogger<LotTakipController> logger) : Controller
{
    private readonly JsonSerializerOptions _jsonOpt = new() { PropertyNameCaseInsensitive = true };
    private HttpClient ApiClient()
    {
        HttpClient? client = httpClientFactory.CreateClient("API");
        string? token = HttpContext.Session.GetString("Token");
        if (!string.IsNullOrEmpty(token))
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
    [HttpGet]
    public IActionResult Index() => View();

    [HttpGet]
    public async Task<IActionResult> GetData()
    {
        try
        {
            HttpClient client = ApiClient();
            HttpResponseMessage? resp = await client.GetAsync("/api/reports/lot-takip");
            string? body = await resp.Content.ReadAsStringAsync();
            return Content(body, "application/json");
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }
    [HttpPost]
    public async Task<IActionResult> CacheSifirla()
    {
        try
        {
            HttpClient client = ApiClient();
            HttpResponseMessage resp = await client.PostAsync("/api/reports/lot-takip/cache-sifirla", null);
            string? body = await resp.Content.ReadAsStringAsync();
            return Content(body, "application/json");
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }
}