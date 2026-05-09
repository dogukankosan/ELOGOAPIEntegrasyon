using Dapper;
using EBelgeAPI.Data.Interfaces;
using EBelgeAPI.Models.Entities;
using EBelgeAPI.Services;
using EBelgeAPI.Services.Interfaces;
using Microsoft.Data.SqlClient;

namespace EBelgeAPI.Data.Repositories;
public class ELogoSettingsRepository : IELogoSettingsRepository
{
    private readonly string _connStr;
    private readonly IEncryptionService _enc;
    public ELogoSettingsRepository(string connStr, IEncryptionService enc)
    {
        _connStr = connStr;
        _enc = enc;
    }
    public async Task<ELogoSettings?> GetActiveAsync()
    {
        const string sql = "SELECT TOP 1 * FROM ELogoSettings ORDER BY Id";
        using var conn = new SqlConnection(_connStr);
        ELogoSettings? row = await conn.QueryFirstOrDefaultAsync<ELogoSettings>(sql);
        if (row == null) return null;
        // Şifre çöz
        row.Url = _enc.Decrypt(row.Url);
        row.Username = _enc.Decrypt(row.Username);
        row.Password = _enc.Decrypt(row.Password);
        return row;
    }
}