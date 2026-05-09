namespace EBelgeAPI.Models.DTOs;
public class SalesInvoiceLineDto
{
    public int LineNo { get; set; }

    // UBL alanları
    public string? Barkod { get; set; }           // SellersItemIdentification/ID
    public string? ManufacturerKodu { get; set; } // ManufacturersItemIdentification/ID
    public string? UrunAdi { get; set; }          // cbc:Name ("MAL", "HİZMET", ürün adı)
    public string? Aciklama { get; set; }         // cbc:Description
    public decimal Miktar { get; set; }
    public string? BirimKodu { get; set; }
    public decimal BirimFiyat { get; set; }
    public decimal KdvOrani { get; set; }
    public decimal KdvTutar { get; set; }
    public decimal SatirToplam { get; set; }      // KDV hariç satır toplam
    public decimal IskontoTutar { get; set; }     // AllowanceCharge Amount
    public string? SeriNo { get; set; }           // cbc:Note (IMEI/Seri)

    // Logo eşleştirme (hesaplanan)
    // Öncelik: Barkod → ManufacturerKodu → "HİZMET"
    public string LogoMalzemeKodu =>
        !string.IsNullOrWhiteSpace(Barkod) ? Barkod.Trim() :
        !string.IsNullOrWhiteSpace(ManufacturerKodu) ? ManufacturerKodu.Trim() :
        "HİZMET";
}