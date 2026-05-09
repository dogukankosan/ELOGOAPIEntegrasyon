using EBelgeAPI.Models.Entities;
using EBelgeAPI.Models.Requests;

namespace EBelgeAPI.Data.Interfaces;
public interface ILogoSettingsRepository
{
    Task<LogoSettings?> GetActiveSettingsAsync();
}
public interface ILogoTokenRepository
{
    Task<LogoTokenCache?> GetValidTokenAsync();
    Task SaveTokenAsync(LogoTokenCache token);
    Task ClearAllAsync();
}
public interface IRevokedTokenRepository
{
    Task RevokeAsync(string jti, string username, DateTime expiresAt);
    Task<bool> IsRevokedAsync(string jti);
    Task CleanupExpiredAsync();
}