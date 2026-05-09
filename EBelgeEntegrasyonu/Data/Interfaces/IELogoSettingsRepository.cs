using EBelgeAPI.Models.Entities;

namespace EBelgeAPI.Data.Interfaces;
public interface IELogoSettingsRepository
{
    Task<ELogoSettings?> GetActiveAsync();
}