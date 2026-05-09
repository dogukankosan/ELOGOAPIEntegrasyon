using EBelgeAPI.Models.Entities;

namespace EBelgeAPI.Data.Interfaces;

public interface IAmbarRepository
{
    Task<List<Ambar>> GetAllAsync(bool? isActive = null);
    Task<Ambar?> GetByIdAsync(int id);
    Task<int> CreateAsync(Ambar ambar);
    Task UpdateAsync(Ambar ambar); 
    Task<bool> KodMevcutMuAsync(string kod, int? excludeId = null);
    Task<Ambar?> GetByKodAsync(string kod);
    Task DeleteAsync(int id);
    Task UpdateAdAsync(int id, string ad);
    Task SetActiveAsync(int id, bool isActive);
}