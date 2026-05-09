using Dapper;
using EBelgeAPI.Data.Interfaces;
using EBelgeAPI.Models.Entities;
using Microsoft.Data.SqlClient;

namespace EBelgeAPI.Data.Repositories;

public class AmbarRepository : IAmbarRepository
{
    private readonly string _conn;
    public AmbarRepository(string conn) => _conn = conn;
    public async Task<List<Ambar>> GetAllAsync(bool? isActive = null)
    {
        using var db = new SqlConnection(_conn);
        var sql = isActive.HasValue
            ? "SELECT * FROM Ambarlar WHERE IsActive = @IsActive ORDER BY Kod"
            : "SELECT * FROM Ambarlar ORDER BY Kod";
        var result = await db.QueryAsync<Ambar>(sql, new { IsActive = isActive });
        return result.ToList();
    }
    public async Task<Ambar?> GetByIdAsync(int id)
    {
        using var db = new SqlConnection(_conn);
        return await db.QueryFirstOrDefaultAsync<Ambar>(
            "SELECT * FROM Ambarlar WHERE Id = @Id", new { Id = id });
    }
    public async Task<Ambar?> GetByKodAsync(string kod)
    {
        using var db = new SqlConnection(_conn);
        return await db.QueryFirstOrDefaultAsync<Ambar>(
            "SELECT * FROM Ambarlar WHERE Kod = @Kod AND IsActive = 1",
            new { Kod = kod });
    }
    public async Task<int> CreateAsync(Ambar ambar)
    {
        using var db = new SqlConnection(_conn);
        return await db.ExecuteScalarAsync<int>(@"
            INSERT INTO Ambarlar (Kod, Ad, IsActive, CreatedAt)
            VALUES (@Kod, @Ad, @IsActive, GETDATE());
            SELECT SCOPE_IDENTITY();", ambar);
    }
    public async Task<bool> KodMevcutMuAsync(string kod, int? excludeId = null)
    {
        using var db = new SqlConnection(_conn);
        var sql = excludeId.HasValue
            ? "SELECT COUNT(1) FROM Ambarlar WHERE Kod = @Kod AND Id != @ExcludeId"
            : "SELECT COUNT(1) FROM Ambarlar WHERE Kod = @Kod";
        int count = await db.ExecuteScalarAsync<int>(sql, new { Kod = kod, ExcludeId = excludeId });
        return count > 0;
    }
    public async Task UpdateAsync(Ambar ambar)
    {
        using var db = new SqlConnection(_conn);
        await db.ExecuteAsync(@"
            UPDATE Ambarlar SET
                Kod = @Kod,
                Ad = @Ad,
                UpdatedAt = GETDATE()
            WHERE Id = @Id", ambar);
    }
    public async Task UpdateAdAsync(int id, string ad)
    {
        using var db = new SqlConnection(_conn);
        await db.ExecuteAsync(@"
        UPDATE Ambarlar SET Ad = @Ad, UpdatedAt = GETDATE()
        WHERE Id = @Id", new { Id = id, Ad = ad });
    }
    public async Task DeleteAsync(int id)
    {
        using var db = new SqlConnection(_conn);
        await db.ExecuteAsync("DELETE FROM Ambarlar WHERE Id = @Id", new { Id = id });
    }
    public async Task SetActiveAsync(int id, bool isActive)
    {
        using var db = new SqlConnection(_conn);
        await db.ExecuteAsync(@"
            UPDATE Ambarlar SET IsActive = @IsActive, UpdatedAt = GETDATE()
            WHERE Id = @Id", new { Id = id, IsActive = isActive });
    }
}