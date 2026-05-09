using Dapper;
using EBelgeAPI.Data.Interfaces;
using EBelgeAPI.Models.Entities;
using EBelgeAPI.Models.Requests;
using Microsoft.Data.SqlClient;

namespace EBelgeAPI.Data.Repositories;

public class ApiLogRepository : IApiLogRepository
{
    private readonly string _conn;
    public ApiLogRepository(string conn) => _conn = conn;
    public async Task WriteAsync(ApiLog log)
    {
        using var db = new SqlConnection(_conn);
        await db.ExecuteAsync(@"
        INSERT INTO ApiLogs 
            (Level, Source, Method, Path, StatusCode, Message, Detail, Username, IpAddress, Duration, CreatedAt)
        VALUES 
            (@Level, @Source, @Method, @Path, @StatusCode, @Message, @Detail, @Username, @IpAddress, @Duration, @CreatedAt)",
            log);
    }
    public async Task<(List<ApiLog> Data, int TotalCount)> GetListAsync(ApiLogFilterRequest f)
    {
        using var db = new SqlConnection(_conn);
        List<string> where = new List<string>();
        DynamicParameters p = new DynamicParameters();
        if (!string.IsNullOrWhiteSpace(f.Level))
        { where.Add("Level = @Level"); p.Add("Level", f.Level); }
        if (!string.IsNullOrWhiteSpace(f.Source))
        { where.Add("Source = @Source"); p.Add("Source", f.Source); }
        if (!string.IsNullOrWhiteSpace(f.Username))
        { where.Add("Username = @Username"); p.Add("Username", f.Username); }
        if (!string.IsNullOrWhiteSpace(f.Path))
        { where.Add("Path LIKE @Path"); p.Add("Path", "%" + f.Path + "%"); }
        if (f.StatusCode.HasValue)
        { where.Add("StatusCode = @StatusCode"); p.Add("StatusCode", f.StatusCode); }
        if (f.BaslangicTarihi.HasValue)
        { where.Add("CreatedAt >= @Baslangic"); p.Add("Baslangic", f.BaslangicTarihi.Value.Date); }
        if (f.BitisTarihi.HasValue)
        { where.Add("CreatedAt < @Bitis"); p.Add("Bitis", f.BitisTarihi.Value.Date.AddDays(1)); }
        string whereStr = where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : "";
        string countSql = $"SELECT COUNT(1) FROM ApiLogs {whereStr}";
        int totalCount = await db.ExecuteScalarAsync<int>(countSql, p);
        p.Add("Offset", (f.Page - 1) * f.PageSize);
        p.Add("PageSize", f.PageSize);
        string dataSql = $@"
            SELECT * FROM ApiLogs {whereStr}
            ORDER BY CreatedAt DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";
        var data = await db.QueryAsync<ApiLog>(dataSql, p);
        return (data.ToList(), totalCount);
    }
    public async Task DeleteAsync(int id)
    {
        using var db = new SqlConnection(_conn);
        await db.ExecuteAsync("DELETE FROM ApiLogs WHERE Id = @Id", new { Id = id });
    }
    public async Task DeleteAllAsync(ApiLogFilterRequest? filter = null)
    {
        using var db = new SqlConnection(_conn);
        if (filter == null)
        {
            await db.ExecuteAsync("DELETE FROM ApiLogs");
            return;
        }
        List<string> where = new List<string>();
        DynamicParameters p = new DynamicParameters();
        if (!string.IsNullOrWhiteSpace(filter.Level))
        { where.Add("Level = @Level"); p.Add("Level", filter.Level); }
        if (!string.IsNullOrWhiteSpace(filter.Source))
        { where.Add("Source = @Source"); p.Add("Source", filter.Source); }
        if (filter.BaslangicTarihi.HasValue)
        { where.Add("CreatedAt >= @Baslangic"); p.Add("Baslangic", filter.BaslangicTarihi.Value.Date); }
        if (filter.BitisTarihi.HasValue)
        { where.Add("CreatedAt < @Bitis"); p.Add("Bitis", filter.BitisTarihi.Value.Date.AddDays(1)); }
       string whereStr = where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : "";
        await db.ExecuteAsync($"DELETE FROM ApiLogs {whereStr}", p);
    }
    public async Task<List<string>> GetDistinctSourcesAsync()
    {
        using var db = new SqlConnection(_conn);
        var result = await db.QueryAsync<string>(
            "SELECT DISTINCT Source FROM ApiLogs WHERE Source IS NOT NULL ORDER BY Source");
        return result.ToList();
    }
    public async Task<List<string>> GetDistinctUsernamesAsync()
    {
        using var db = new SqlConnection(_conn);
        var result = await db.QueryAsync<string>(
            "SELECT DISTINCT Username FROM ApiLogs WHERE Username IS NOT NULL ORDER BY Username");
        return result.ToList();
    }
}