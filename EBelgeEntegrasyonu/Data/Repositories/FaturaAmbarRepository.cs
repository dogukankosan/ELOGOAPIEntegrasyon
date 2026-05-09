using Dapper;
using EBelgeAPI.Data.Interfaces;
using Microsoft.Data.SqlClient;

namespace EBelgeAPI.Data.Repositories;

public class FaturaAmbarRepository : IFaturaAmbarRepository
{
    private readonly string _conn;
    public FaturaAmbarRepository(string conn) => _conn = conn;
    public async Task<int?> GetAmbarIdByUuidAsync(string uuid)
    {
        using var db = new SqlConnection(_conn);
        var result = await db.ExecuteScalarAsync<int?>(
            "SELECT AmbarId FROM FaturaAmbar WHERE ELogoUuid = @uuid",
            new { uuid });
        return result;
    }
    public async Task UpsertAsync(string uuid, int ambarId)
    {
        using var db = new SqlConnection(_conn);
        await db.ExecuteAsync(@"
            IF EXISTS (SELECT 1 FROM FaturaAmbar WHERE ELogoUuid = @uuid)
                UPDATE FaturaAmbar SET AmbarId = @ambarId, UpdatedAt = GETDATE()
                WHERE ELogoUuid = @uuid
            ELSE
                INSERT INTO FaturaAmbar (ELogoUuid, AmbarId, CreatedAt)
                VALUES (@uuid, @ambarId, GETDATE())",
            new { uuid, ambarId });
    }
    public async Task<bool> IsAmbarUsedAsync(int ambarId)
    {
        using var db = new SqlConnection(_conn);
        var count = await db.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM FaturaAmbar WHERE AmbarId = @ambarId",
            new { ambarId });
        return count > 0;
    }
    public async Task<Dictionary<string, int>> GetAmbarMapByUuidsAsync(List<string> uuids)
    {
        if (uuids == null || uuids.Count == 0)
            return new Dictionary<string, int>();
        using var db = new SqlConnection(_conn);
        var rows = await db.QueryAsync<(string Uuid, int AmbarId)>(
            "SELECT ELogoUuid, AmbarId FROM FaturaAmbar WHERE ELogoUuid IN @uuids",
            new { uuids });
        return rows.ToDictionary(r => r.Uuid, r => r.AmbarId);
    }
}