using EBelgeAPI.Data.Interfaces;
using EBelgeAPI.Models.Entities;
using EBelgeAPI.Services;
using EBelgeAPI.Services.Interfaces;

public class LogService(IApiLogRepository logRepo) : ILogService
{
    public Task InfoAsync(string message, string source = "API", string? path = null,
        string? method = null, int? statusCode = null, string? username = null,
        string? ip = null, int? duration = null, string? detail = null)
        => WriteAsync("INFO", message, source, path, method, statusCode, username, ip, duration, detail);

    public Task WarningAsync(string message, string source = "API", string? path = null,
        string? method = null, int? statusCode = null, string? username = null,
        string? ip = null, int? duration = null, string? detail = null)
        => WriteAsync("WARNING", message, source, path, method, statusCode, username, ip, duration, detail);

    public Task ErrorAsync(string message, string source = "API", string? path = null,
        string? method = null, int? statusCode = null, string? username = null,
        string? ip = null, int? duration = null, string? detail = null)
        => WriteAsync("ERROR", message, source, path, method, statusCode, username, ip, duration, detail);

    private Task WriteAsync(string level, string message, string source, string? path,
        string? method, int? statusCode, string? username, string? ip, int? duration, string? detail)
        => logRepo.WriteAsync(new ApiLog
        {
            Level = level,
            Source = source,
            Path = path,
            Method = method,
            StatusCode = statusCode,
            Message = message,
            Detail = SensitiveDataMasker.Mask(detail),
            Username = username,
            IpAddress = ip,
            Duration = duration,
            CreatedAt = DateTime.Now
        });
}