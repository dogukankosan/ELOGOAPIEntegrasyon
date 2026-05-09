using Dapper;
using EBelgeAPI.Data.Interfaces;
using EBelgeAPI.Models.DTOs;
using Microsoft.Data.SqlClient;

namespace EBelgeAPI.Data.Repositories;

public class DashboardRepository : IDashboardRepository
{
    private readonly string _conn;
    public DashboardRepository(string conn) => _conn = conn;
    public async Task<DashboardStatsDto> GetStatsAsync()
    {
        using var db = new SqlConnection(_conn);
        DashboardStatsDto? stats = await db.QueryFirstAsync<DashboardStatsDto>(@"
            SELECT
                -- Toplam transfer sayıları
                COUNT(CASE WHEN AktarimDurumu = 1 THEN 1 END) AS ToplamAktarilan,
                COUNT(CASE WHEN AktarimDurumu = 2 THEN 1 END) AS ToplamHatali,
                COUNT(CASE WHEN AktarimDurumu = 0 THEN 1 END) AS ToplamBekleyen,
 
                -- Bugünkü transfer sayıları
                COUNT(CASE WHEN AktarimDurumu = 1
                    AND CAST(AktarimTarihi AS DATE) = CAST(GETDATE() AS DATE) THEN 1 END) AS BugunkuAktarilan,
                COUNT(CASE WHEN AktarimDurumu = 2
                    AND CAST(AktarimTarihi AS DATE) = CAST(GETDATE() AS DATE) THEN 1 END) AS BugunkuHatali,
 
                -- Son 7 gün
                COUNT(CASE WHEN AktarimDurumu = 1
                    AND AktarimTarihi >= DATEADD(DAY, -7, GETDATE()) THEN 1 END) AS Son7GunAktarilan,
                COUNT(CASE WHEN AktarimDurumu = 2
                    AND AktarimTarihi >= DATEADD(DAY, -7, GETDATE()) THEN 1 END) AS Son7GunHatali
 
            FROM ELogoLogoTransfers");
        // Kara liste sayısı
        stats.KaraListeSayisi = await db.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM CariFiltre WHERE IsActive = 1");
        // Ambar sayısı
        stats.AmbarSayisi = await db.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM Ambarlar WHERE IsActive = 1");
        return stats;
    }
    public async Task<List<DashboardTransferDto>> GetSonHataliTransferlerAsync(int limit = 5)
    {
        using var db = new SqlConnection(_conn);
        var result = await db.QueryAsync<DashboardTransferDto>(@"
            SELECT TOP (@Limit)
                ELogoFaturaNo AS FaturaNo,
                ELogoUuid     AS Uuid,
                HataMesaji,
                AktarimTarihi,
                CreatedAt
            FROM ELogoLogoTransfers
            WHERE AktarimDurumu = 2
              AND HataMesaji IS NOT NULL
            ORDER BY Id DESC",
            new { Limit = limit });
        return result.ToList();
    }
    public async Task<List<DashboardTransferDto>> GetSonBasariliTransferlerAsync(int limit = 5)
    {
        using var db = new SqlConnection(_conn);
        var result = await db.QueryAsync<DashboardTransferDto>(@"
            SELECT TOP (@Limit)
                ELogoFaturaNo AS FaturaNo,
                LogoFaturaNo,
                ELogoUuid     AS Uuid,
                AktarimTarihi,
                LogoLogicalRef
            FROM ELogoLogoTransfers
            WHERE AktarimDurumu = 1
            ORDER BY AktarimTarihi DESC",
            new { Limit = limit });
        return result.ToList();
    }
}