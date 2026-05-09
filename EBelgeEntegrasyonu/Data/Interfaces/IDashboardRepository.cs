using EBelgeAPI.Models.DTOs;

namespace EBelgeAPI.Data.Interfaces;

public interface IDashboardRepository
{
    Task<DashboardStatsDto> GetStatsAsync();
    Task<List<DashboardTransferDto>> GetSonHataliTransferlerAsync(int limit = 5);
    Task<List<DashboardTransferDto>> GetSonBasariliTransferlerAsync(int limit = 5);
}