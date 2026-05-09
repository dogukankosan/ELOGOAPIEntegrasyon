using Dapper;
using EBelgeAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Text.Json;

namespace EBelgeAPI.Controllers;
[AllowAnonymous]  // ← ekle
[ApiController]
[Route("api/settings")]
public class SettingsController(
    IConfiguration config,
    IEncryptionService enc,
    ILogger<SettingsController> logger) : ControllerBase
{
    // ── Admin key kontrolü ────────────────────────────────
    private bool AdminKeyGecerli()
    {
        string? key = Request.Headers["X-Settings-Key"].FirstOrDefault();
        string? expected = config["Settings:AdminKey"];
        return !string.IsNullOrEmpty(expected) && key == expected;
    }
    private string Conn => config.GetConnectionString("DefaultConnection")!;

    // ── GET: Tüm ayarları getir (şifreli alanlar çözülmüş) ──
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        if (!AdminKeyGecerli())
            return Unauthorized(new { success = false, message = "Geçersiz admin key." });
        try
        {
            using var db = new SqlConnection(Conn);
            var logo = await db.QueryFirstOrDefaultAsync(@"
                SELECT Id, ServerUrl, MachineId, FirmNr, ClientId, ClientSecret,
                       CustomerId, LogoUsername, LogoPassword, IsActive, CreatedAt
                FROM LogoSettings WHERE IsActive = 1");
            var elogo = await db.QueryFirstOrDefaultAsync(@"
                SELECT Id, Url, Username, Password, CreatedAt, UpdatedAt
                FROM ELogoSettings");
            return Ok(new
            {
                success = true,
                data = new
                {
                    logo = logo == null ? null : new
                    {
                        id = (int)logo.Id,
                        serverUrl = TryDecrypt(logo.ServerUrl),
                        machineId = TryDecrypt(logo.MachineId),
                        firmNr = TryDecrypt(logo.FirmNr),
                        clientId = TryDecrypt(logo.ClientId),
                        clientSecret = TryDecrypt(logo.ClientSecret),
                        customerId = TryDecrypt(logo.CustomerId),
                        logoUsername = TryDecrypt(logo.LogoUsername),
                        logoPassword = TryDecrypt(logo.LogoPassword),
                        isActive = (bool)logo.IsActive,
                        createdAt = (DateTime)logo.CreatedAt
                    },
                    elogo = elogo == null ? null : new
                    {
                        id = (int)elogo.Id,
                        url = TryDecrypt(elogo.Url),
                        username = TryDecrypt(elogo.Username),
                        password = TryDecrypt(elogo.Password),
                        createdAt = (DateTime)elogo.CreatedAt,
                        updatedAt = (DateTime?)elogo.UpdatedAt
                    }
                }
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Settings GET hatasi");
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }
    // ── PUT: Logo ayarlarını güncelle ─────────────────────
    [HttpPut("logo")]
    public async Task<IActionResult> UpdateLogo([FromBody] LogoSettingsUpdateRequest req)
    {
        if (!AdminKeyGecerli())
            return Unauthorized(new { success = false, message = "Geçersiz admin key." });
        try
        {
            using var db = new SqlConnection(Conn);
            await db.ExecuteAsync(@"
                UPDATE LogoSettings SET
                    ServerUrl    = @ServerUrl,
                    MachineId    = @MachineId,
                    FirmNr       = @FirmNr,
                    ClientId     = @ClientId,
                    ClientSecret = @ClientSecret,
                    CustomerId   = @CustomerId,
                    LogoUsername = @LogoUsername,
                    LogoPassword = @LogoPassword
                WHERE IsActive = 1",
                new
                {
                    ServerUrl = enc.Encrypt(req.ServerUrl),
                    MachineId = enc.Encrypt(req.MachineId),
                    FirmNr = enc.Encrypt(req.FirmNr),
                    ClientId = enc.Encrypt(req.ClientId),
                    ClientSecret = enc.Encrypt(req.ClientSecret),
                    CustomerId = enc.Encrypt(req.CustomerId),
                    LogoUsername = enc.Encrypt(req.LogoUsername),
                    LogoPassword = enc.Encrypt(req.LogoPassword)
                });
            logger.LogInformation("Logo ayarları güncellendi.");
            return Ok(new { success = true, message = "Logo ayarları güncellendi." });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Logo ayarlari güncelleme hatasi");
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }
    // ── PUT: e-Logo ayarlarını güncelle ───────────────────
    [HttpPut("elogo")]
    public async Task<IActionResult> UpdateELogo([FromBody] ELogoSettingsUpdateRequest req)
    {
        if (!AdminKeyGecerli())
            return Unauthorized(new { success = false, message = "Geçersiz admin key." });
        try
        {
            using var db = new SqlConnection(Conn);
            await db.ExecuteAsync(@"
                UPDATE ELogoSettings SET
                    Url       = @Url,
                    Username  = @Username,
                    Password  = @Password,
                    UpdatedAt = GETDATE()
                WHERE Id = (SELECT TOP 1 Id FROM ELogoSettings ORDER BY Id DESC)",
                new
                {
                    Url = enc.Encrypt(req.Url),
                    Username = enc.Encrypt(req.Username),
                    Password = enc.Encrypt(req.Password)
                });
            logger.LogInformation("e-Logo ayarları güncellendi.");
            return Ok(new { success = true, message = "e-Logo ayarları güncellendi." });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "e-Logo ayarlari güncelleme hatasi");
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }
    // ── Şifre çözme helper ────────────────────────────────
    private string TryDecrypt(object? val)
    {
        if (val == null || val is DBNull) return "";
        try { return enc.Decrypt(val.ToString()!); }
        catch { return val.ToString()!; }
    }
}
// ── Request modelleri ─────────────────────────────────────
public class LogoSettingsUpdateRequest
{
    public string ServerUrl { get; set; } = "";
    public string MachineId { get; set; } = "";
    public string FirmNr { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string CustomerId { get; set; } = "";
    public string LogoUsername { get; set; } = "";
    public string LogoPassword { get; set; } = "";
}
public class ELogoSettingsUpdateRequest
{
    public string Url { get; set; } = "";
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
}