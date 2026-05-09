using EBelgeUI.Filters;
using EBelgeUI.Models;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Text.Json;

namespace EBelgeUI.Controllers;

[SessionAuthFilter]
public class DashboardController(
    IHttpClientFactory httpClientFactory,
    ILogger<DashboardController> logger) : Controller
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
    [SkipSessionAuth]
    public IActionResult HttpError(int code)
    {
        Response.StatusCode = code;
        ViewBag.StatusCode = code;
        return View();
    }
    [HttpGet]
    [SkipSessionAuth]
    public IActionResult Error() => View();
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        DashboardViewModel model = new DashboardViewModel();
        try
        {
            HttpClient client = ApiClient();
            var statsTask = client.GetAsync("/api/dashboard/stats");
            var hataliTask = client.GetAsync("/api/dashboard/son-hatali?limit=8");
            var basariliTask = client.GetAsync("/api/dashboard/son-basarili?limit=8");
            await Task.WhenAll(statsTask, hataliTask, basariliTask);
            if (statsTask.Result.IsSuccessStatusCode)
            {
                string body = await statsTask.Result.Content.ReadAsStringAsync();
                DashboardStatsApiResponse? result = JsonSerializer.Deserialize<DashboardStatsApiResponse>(body, _jsonOpt);
                if (result?.Success == true)
                    model.Stats = result.Data;
            }
            if (hataliTask.Result.IsSuccessStatusCode)
            {
                string body = await hataliTask.Result.Content.ReadAsStringAsync();
                DashboardTransferApiResponse? result = JsonSerializer.Deserialize<DashboardTransferApiResponse>(body, _jsonOpt);
                if (result?.Success == true)
                    model.SonHataliTransferler = result.Data ?? [];
            }
            if (basariliTask.Result.IsSuccessStatusCode)
            {
                string? body = await basariliTask.Result.Content.ReadAsStringAsync();
                DashboardTransferApiResponse? result = JsonSerializer.Deserialize<DashboardTransferApiResponse>(body, _jsonOpt);
                if (result?.Success == true)
                    model.SonBasariliTransferler = result.Data ?? [];
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Dashboard Index hatasi");
            model.ErrorMessage = "API'ye baglanýlamadý.";
        }
        return View(model);
    }
}