namespace EBelgeAPI.Services.Interfaces;

public interface ILogoItemCacheService
{
    /// <summary>
    /// Malzeme koduna göre cardType döndürür.
    /// 1 = Mal, 30 = Hizmet, null = bulunamadı
    /// </summary>
    Task<int?> GetCardTypeAsync(string itemCode);

    /// <summary>
    /// Cache'i yeniler (background job veya manuel)
    /// </summary>
    Task RefreshAsync();
}