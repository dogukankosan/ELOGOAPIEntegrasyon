using EBelgeAPI.Models.Entities;

namespace EBelgeAPI.Data.Interfaces;

public interface ICariFilterRepository
{
    Task<bool> KimlikNoMevcutMuAsync(string kimlikNo);
    Task<List<CariFiltre>> GetAllAsync();
    Task<HashSet<string>> GetActiveKimlikNoSetAsync(); // performanslı lookup
    Task<int> CreateAsync(CariFiltre filtre);
    Task DeleteAsync(int id);
    Task SetActiveAsync(int id, bool isActive);
}