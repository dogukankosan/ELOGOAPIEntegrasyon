using EBelgeAPI.Models.Entities;

namespace EBelgeAPI.Data.Interfaces;
public interface ITransferRepository
{
    Task<Dictionary<string, string>> GetHataMapByUuidsAsync(List<string> uuids);
    Task<bool> IsTransferredAsync(string uuid);
    Task<int> CreateAsync(ELogoLogoTransfer transfer);
    Task UpdateAsync(ELogoLogoTransfer transfer); 
    Task<ELogoLogoTransfer?> GetByUuidAsync(string uuid);
    Task<List<string>> GetTransferredUuidsAsync();
    Task<ELogoLogoTransfer?> GetBasariliByUuidAsync(string uuid);
}