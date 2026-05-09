using Dapper;
using EBelgeAPI.Data.Interfaces;
using Microsoft.Data.SqlClient;

namespace EBelgeAPI.Data.Repositories;

public class FaturaSatisElemaniRepository : IFaturaSatisElemaniRepository
{
    private readonly string _conn;
    public FaturaSatisElemaniRepository(string conn) => _conn = conn;
    public async Task<string?> GetByUuidAsync(string uuid)
    {
        using var db = new SqlConnection(_conn);
        return await db.ExecuteScalarAsync<string?>(
            "SELECT SatisElemaniKodu FROM FaturaSatisElemani WHERE ELogoUuid = @uuid",
            new { uuid });
    }
    public async Task UpsertAsync(string uuid, string satisElemaniKodu)
    {
        using var db = new SqlConnection(_conn);
        await db.ExecuteAsync(@"
            IF EXISTS (SELECT 1 FROM FaturaSatisElemani WHERE ELogoUuid = @uuid)
                UPDATE FaturaSatisElemani 
                SET SatisElemaniKodu = @satisElemaniKodu, UpdatedAt = GETDATE()
                WHERE ELogoUuid = @uuid
            ELSE
                INSERT INTO FaturaSatisElemani (ELogoUuid, SatisElemaniKodu, CreatedAt)
                VALUES (@uuid, @satisElemaniKodu, GETDATE())",
            new { uuid, satisElemaniKodu });
    }
    public async Task<Dictionary<string, string>> GetMapByUuidsAsync(List<string> uuids)
    {
        if (uuids == null || uuids.Count == 0)
            return new Dictionary<string, string>();
        using var db = new SqlConnection(_conn);
        var rows = await db.QueryAsync<(string Uuid, string Kod)>(
            "SELECT ELogoUuid, SatisElemaniKodu FROM FaturaSatisElemani WHERE ELogoUuid IN @uuids",
            new { uuids });
        return rows.ToDictionary(r => r.Uuid, r => r.Kod);
    }
}