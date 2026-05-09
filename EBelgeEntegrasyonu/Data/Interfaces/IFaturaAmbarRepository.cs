namespace EBelgeAPI.Data.Interfaces;

public interface IFaturaAmbarRepository
{
    Task<int?> GetAmbarIdByUuidAsync(string uuid);
    Task UpsertAsync(string uuid, int ambarId);
    Task<bool> IsAmbarUsedAsync(int ambarId);
    Task<Dictionary<string, int>> GetAmbarMapByUuidsAsync(List<string> uuids);
}