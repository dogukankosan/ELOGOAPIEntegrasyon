namespace EBelgeAPI.Data.Interfaces;

public interface IFaturaSatisElemaniRepository
{
    Task<string?> GetByUuidAsync(string uuid);
    Task UpsertAsync(string uuid, string satisElemaniKodu);
    Task<Dictionary<string, string>> GetMapByUuidsAsync(List<string> uuids);
}