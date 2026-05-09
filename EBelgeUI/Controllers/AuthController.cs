using EBelgeUI.Filters;
using EBelgeUI.Models;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;

namespace EBelgeUI.Controllers;

[SkipSessionAuth]
public class AuthController(IHttpClientFactory httpClientFactory) : Controller
{
    private readonly JsonSerializerOptions _jsonOpt = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [HttpGet]
    public IActionResult Login()
    {
        if (!string.IsNullOrEmpty(HttpContext.Session.GetString("Token")))
            return RedirectToAction("Index", "Dashboard");
        // Cookie'den hatırla
        string? rememberToken = Request.Cookies["RememberToken"];
        string? rememberUser = Request.Cookies["RememberUser"];
        if (!string.IsNullOrEmpty(rememberToken) && !string.IsNullOrEmpty(rememberUser))
        {
            HttpContext.Session.SetString("Token", rememberToken);
            HttpContext.Session.SetString("Username", rememberUser);
            HttpContext.Session.SetString("ExpiresAt", DateTime.UtcNow.AddDays(7).ToString("o"));
            return RedirectToAction("Index", "Dashboard");
        }
        return View(new LoginViewModel());
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Username) || string.IsNullOrWhiteSpace(model.Password))
        {
            model.ErrorMessage = "Kullanıcı adı ve şifre zorunludur.";
            return View(model);
        }
        try
        {
            HttpClient client = httpClientFactory.CreateClient("API");
            string? payload = JsonSerializer.Serialize(new { username = model.Username, password = model.Password });
            HttpResponseMessage resp = await client.PostAsync("/api/auth/login",
                new StringContent(payload, Encoding.UTF8, "application/json"));
            string? body = await resp.Content.ReadAsStringAsync();
            LoginResponse? result = JsonSerializer.Deserialize<LoginResponse>(body, _jsonOpt);
            if (!resp.IsSuccessStatusCode || result?.Success != true || result.Data == null)
            {
                model.ErrorMessage = "Kullanıcı adı veya şifre hatalı. Lütfen tekrar deneyin.";
                model.Password = string.Empty;
                return View(model);
            }
            HttpContext.Session.SetString("Token", result.Data.Token);
            HttpContext.Session.SetString("Username", result.Data.FullName);
            HttpContext.Session.SetString("ExpiresAt", result.Data.ExpiresAt.ToString("o"));
            // Beni hatırla
            if (model.RememberMe)
            {
                CookieOptions cookieOptions = new CookieOptions
                {
                    Expires = DateTimeOffset.UtcNow.AddDays(7),
                    HttpOnly = true,
                    SameSite = SameSiteMode.Strict
                };
                Response.Cookies.Append("RememberToken", result.Data.Token, cookieOptions);
                Response.Cookies.Append("RememberUser", result.Data.FullName, cookieOptions);
            }
            return RedirectToAction("Index", "Dashboard");
        }
        catch
        {
            model.ErrorMessage = "API'ye bağlanılamadı. Lütfen daha sonra tekrar deneyin.";
            model.Password = string.Empty;
            return View(model);
        }
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        string? token = HttpContext.Session.GetString("Token");
        if (!string.IsNullOrEmpty(token))
        {
            try
            {
                HttpClient? client = httpClientFactory.CreateClient("API");
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
                request.Headers.Add("Authorization", $"Bearer {token}");
                await client.SendAsync(request);
            }
            catch { }
        }
        HttpContext.Session.Clear();
        Response.Cookies.Delete("RememberToken");
        Response.Cookies.Delete("RememberUser");
        return RedirectToAction("Login");
    }
}