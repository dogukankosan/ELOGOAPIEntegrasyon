using Dapper;
using EBelgeAPI.Data.Interfaces;
using EBelgeAPI.Models.Entities;
using Microsoft.Data.SqlClient;
using System.Text.Json;

namespace EBelgeAPI.Data.Repositories;

public class TransferRepository : ITransferRepository
{
    private readonly string _conn;
    public TransferRepository(string conn) => _conn = conn;
    // HataMesaji'ndan okunabilir kısmı çıkar
    private static string SadeleHata(string raw)
    {
        try
        {
            // {"message":["...mesaj...\n"]} formatından çıkar
            JsonDocument doc = System.Text.Json.JsonDocument.Parse(raw
                .Replace("Logo ERP hata: ", ""));
            string? msg = doc.RootElement
                .GetProperty("message")[0]
                .GetString() ?? raw;
            // XUIControllerException: Hata Oluştu: kısmını temizle
            int idx = msg.IndexOf("Hata Oluştu:");
            if (idx >= 0) msg = msg[(idx + 12)..].Trim();
            // \n ve uzun stack trace temizle
            return msg.Split('\n')[0].Trim();
        }
        catch { return raw; }
    }
    public async Task<Dictionary<string, string>> GetHataMapByUuidsAsync(List<string> uuids)
    {
        if (uuids == null || uuids.Count == 0)
            return new Dictionary<string, string>();
        using var db = new SqlConnection(_conn);
        var rows = await db.QueryAsync<(string Uuid, string Hata)>(@"
        SELECT ELogoUuid, HataMesaji
        FROM ELogoLogoTransfers t1
        WHERE ELogoUuid IN @uuids
          AND AktarimDurumu = 2
          AND HataMesaji IS NOT NULL
          AND Id = (
              SELECT MAX(Id) FROM ELogoLogoTransfers t2
              WHERE t2.ELogoUuid = t1.ELogoUuid
                AND t2.AktarimDurumu = 2
          )",
            new { uuids });
        return rows.ToDictionary(
      r => r.Uuid,
      r => SadeleHata(r.Hata));
    }
    public async Task<bool> IsTransferredAsync(string uuid)
    {
        using var db = new SqlConnection(_conn);
        var count = await db.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM ELogoLogoTransfers WHERE ELogoUuid=@uuid AND AktarimDurumu=1",
            new { uuid });
        return count > 0;
    }
    public async Task<ELogoLogoTransfer?> GetBasariliByUuidAsync(string uuid)
    {
        using var db = new SqlConnection(_conn);
        return await db.QueryFirstOrDefaultAsync<ELogoLogoTransfer>(@"
        SELECT TOP 1 * FROM ELogoLogoTransfers 
        WHERE ELogoUuid = @uuid AND AktarimDurumu = 1",
            new { uuid });
    }
    public async Task<List<string>> GetTransferredUuidsAsync()
    {
        using var db = new SqlConnection(_conn);
        var result = await db.QueryAsync<string>(@"
        SELECT DISTINCT ELogoUuid 
        FROM ELogoLogoTransfers 
        WHERE AktarimDurumu = 1");
        return result.ToList();
    }
    public async Task<int> CreateAsync(ELogoLogoTransfer t)
    {
        using var db = new SqlConnection(_conn);
        return await db.ExecuteScalarAsync<int>(@"
            INSERT INTO ELogoLogoTransfers 
                (ELogoUuid, ELogoFaturaNo, AktarimDurumu, CreatedAt)
            VALUES (@ELogoUuid, @ELogoFaturaNo, @AktarimDurumu, @CreatedAt);
            SELECT SCOPE_IDENTITY();", t);
    }
    public async Task UpdateAsync(ELogoLogoTransfer t)
    {
        using var db = new SqlConnection(_conn);
        await db.ExecuteAsync(@"
            UPDATE ELogoLogoTransfers SET
                LogoFaturaNo = @LogoFaturaNo,
                LogoLogicalRef = @LogoLogicalRef,
                AktarimDurumu = @AktarimDurumu,
                HataMesaji = @HataMesaji,
                AktarimTarihi = @AktarimTarihi
            WHERE Id = @Id", t);
    }
    // GetByUuidAsync — en son AktarimDurumu=1 olanı getir
    public async Task<ELogoLogoTransfer?> GetByUuidAsync(string uuid)
    {
        using var db = new SqlConnection(_conn);
        return await db.QueryFirstOrDefaultAsync<ELogoLogoTransfer>(@"
        SELECT TOP 1 * FROM ELogoLogoTransfers 
        WHERE ELogoUuid = @uuid 
        ORDER BY 
            CASE WHEN AktarimDurumu = 1 THEN 0 ELSE 1 END,
            Id DESC",
            new { uuid });
    }
}