using Dapper;
using EBelgeAPI.Data.Interfaces;
using EBelgeAPI.Models.Entities;
using Microsoft.Data.SqlClient;

namespace EBelgeAPI.Data.Repositories;

public class CariFilterRepository : ICariFilterRepository
{
    private readonly string _conn;
    public CariFilterRepository(string conn) => _conn = conn;
    public async Task<List<CariFiltre>> GetAllAsync()
    {
        using var db = new SqlConnection(_conn);
        var result = await db.QueryAsync<CariFiltre>(
            "SELECT * FROM CariFiltre ORDER BY CreatedAt DESC");
        return result.ToList();
    }
    public async Task<HashSet<string>> GetActiveKimlikNoSetAsync()
    {
        using var db = new SqlConnection(_conn);
        var result = await db.QueryAsync<string>(
            "SELECT KimlikNo FROM CariFiltre WHERE IsActive = 1");
        return result.ToHashSet();
    }
    public async Task<int> CreateAsync(CariFiltre filtre)
    {
        using var db = new SqlConnection(_conn);
        return await db.ExecuteScalarAsync<int>(@"
            INSERT INTO CariFiltre (KimlikNo, KimlikTipi, Ad, Aciklama, IsActive, CreatedAt)
            VALUES (@KimlikNo, @KimlikTipi, @Ad, @Aciklama, @IsActive, GETDATE());
            SELECT SCOPE_IDENTITY();", filtre);
    }
    public async Task DeleteAsync(int id)
    {
        using var db = new SqlConnection(_conn);
        await db.ExecuteAsync("DELETE FROM CariFiltre WHERE Id = @Id", new { Id = id });
    }
    public async Task SetActiveAsync(int id, bool isActive)
    {
        using var db = new SqlConnection(_conn);
        await db.ExecuteAsync(@"
            UPDATE CariFiltre SET IsActive = @IsActive
            WHERE Id = @Id", new { Id = id, IsActive = isActive });
    }
    public async Task<bool> KimlikNoMevcutMuAsync(string kimlikNo)
    {
        using var db = new SqlConnection(_conn);
        var count = await db.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM CariFiltre WHERE KimlikNo = @KimlikNo",
            new { KimlikNo = kimlikNo });
        return count > 0;
    }
}