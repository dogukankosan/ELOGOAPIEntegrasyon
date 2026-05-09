using EBelgeAPI.Models.Entities;
using EBelgeAPI.Models.Requests;
namespace EBelgeAPI.Data.Interfaces;
public interface IApiLogRepository
{
    Task<(List<ApiLog> Data, int TotalCount)> GetListAsync(ApiLogFilterRequest filter);
    Task DeleteAsync(int id);
    Task DeleteAllAsync(ApiLogFilterRequest? filter = null);
    Task<List<string>> GetDistinctSourcesAsync();
    Task<List<string>> GetDistinctUsernamesAsync();
    Task WriteAsync(ApiLog log);
}