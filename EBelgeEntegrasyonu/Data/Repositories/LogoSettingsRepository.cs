using Dapper;
using EBelgeAPI.Data.Interfaces;
using EBelgeAPI.Models.Entities;
using EBelgeAPI.Services;
using Microsoft.Data.SqlClient;

namespace EBelgeAPI.Data.Repositories;

// ── LogoSettings ──────────────────────────────────────────────────────
public class LogoSettingsRepository(string connectionString, IEncryptionService enc)
    : ILogoSettingsRepository
{
    private SqlConnection Conn() => new(connectionString);
    public async Task<LogoSettings?> GetActiveSettingsAsync()
    {
        using var conn = Conn();  
        var raw = await conn.QueryFirstOrDefaultAsync<LogoSettings>(
            "SELECT TOP 1 * FROM dbo.LogoSettings WHERE IsActive = 1");
        if (raw == null) return null;
        raw.ServerUrl = enc.Decrypt(raw.ServerUrl);
        raw.MachineId = enc.Decrypt(raw.MachineId);
        raw.CustomerId = enc.Decrypt(raw.CustomerId);
        raw.FirmNr = enc.Decrypt(raw.FirmNr);
        raw.ClientId = enc.Decrypt(raw.ClientId);
        raw.ClientSecret = enc.Decrypt(raw.ClientSecret);
        raw.LogoUsername = enc.Decrypt(raw.LogoUsername);
        raw.LogoPassword = enc.Decrypt(raw.LogoPassword);
        return raw;
    }
}
// ── LogoTokenCache ────────────────────────────────────────────────────
public class LogoTokenRepository(string connectionString, IEncryptionService enc)
    : ILogoTokenRepository
{
    private SqlConnection Conn() => new(connectionString);
    public async Task<LogoTokenCache?> GetValidTokenAsync()
    {
        using var conn = Conn();
        var raw = await conn.QueryFirstOrDefaultAsync<LogoTokenCache>(
            "SELECT TOP 1 * FROM dbo.LogoTokenCache WHERE ExpireDate > @Now",
            new { Now = DateTime.UtcNow.AddMinutes(5) });
        if (raw == null) return null;
        raw.AccessToken = enc.Decrypt(raw.AccessToken);
        raw.Server = enc.Decrypt(raw.Server);
        raw.Firm = enc.Decrypt(raw.Firm);
        return raw;
    }
    public async Task SaveTokenAsync(LogoTokenCache token)
    {
        using var conn = Conn();
        await conn.ExecuteAsync("DELETE FROM dbo.LogoTokenCache");
        await conn.ExecuteAsync(
            @"INSERT INTO dbo.LogoTokenCache 
                (AccessToken, ExpireDate, Server, Firm, CreatedAt)
              VALUES 
                (@AccessToken, @ExpireDate, @Server, @Firm, @CreatedAt)",
            new
            {
                AccessToken = enc.Encrypt(token.AccessToken),
                token.ExpireDate,
                Server = enc.Encrypt(token.Server),
                Firm = enc.Encrypt(token.Firm),
                token.CreatedAt
            });
    }
    public async Task ClearAllAsync()
    {
        using var conn = Conn();
        await conn.ExecuteAsync("DELETE FROM dbo.LogoTokenCache");
    }
}
public class RevokedTokenRepository(string connectionString) : IRevokedTokenRepository
{
    private SqlConnection Conn() => new(connectionString);
    public async Task RevokeAsync(string jti, string username, DateTime expiresAt)
    {
        using var conn = Conn();
        await conn.ExecuteAsync(
            @"INSERT INTO dbo.RevokedTokens (Jti, Username, ExpiresAt)
              VALUES (@Jti, @Username, @ExpiresAt)",
            new { Jti = jti, Username = username, ExpiresAt = expiresAt });
    }
    public async Task<bool> IsRevokedAsync(string jti)
    {
        using var conn = Conn();
        var result = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM dbo.RevokedTokens WHERE Jti = @Jti AND ExpiresAt > @Now",
            new { Jti = jti, Now = DateTime.UtcNow });
        return result > 0;
    }
    public async Task CleanupExpiredAsync()
    {
        using var conn = Conn();
        await conn.ExecuteAsync(
            "DELETE FROM dbo.RevokedTokens WHERE ExpiresAt < @Now",
            new { Now = DateTime.UtcNow });
    }
}