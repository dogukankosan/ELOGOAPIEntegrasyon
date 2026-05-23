namespace EBelgeAPI.Models.DTOs
{
    public class LotTakipDto
    {
        public string? SeriNo { get; set; }
        public string? MalzemeKodu { get; set; }
        public string? MalzemeAdi { get; set; }

        // Giriş
        public DateTime? GirisTarihi { get; set; }
        public string? GirisFisTipi { get; set; }
        public string? GirisFisNo { get; set; }
        public string? GirisCariKodu { get; set; }
        public string? GirisCariUnvani { get; set; }
        public decimal? GirisBirimFiyat { get; set; }
        public decimal? GirisKdvMatrahi { get; set; }
        public decimal? GirisKdvOrani { get; set; }
        public decimal? GirisKdvTutari { get; set; }
        public decimal? GirisToplamTutar { get; set; }

        // Çıkış (STRING_AGG | ile ayrılmış)
        public string? CikisTarihleri { get; set; }
        public string? CikisFisTipleri { get; set; }
        public string? CikisFisNolari { get; set; }
        public string? CikisCariKodlari { get; set; }
        public string? CikisCariUnvanlari { get; set; }
        public string? CikisBirimFiyatlari { get; set; }
        public string? CikisKdvMatrahlari { get; set; }
        public string? CikisKdvOranlari { get; set; }
        public string? CikisKdvTutarlari { get; set; }
        public string? CikisToplamTutarlari { get; set; }
        // Giriş bölümüne ekle (GirisBirimFiyat'ın altına):
        public decimal? GirisMiktar { get; set; }

        // Çıkış bölümüne ekle (CikisBirimFiyatlari'nin üstüne):
        public string? CikisMiktarlari { get; set; }
        // Özet
        public decimal ToplamGiris { get; set; }
        public decimal ToplamCikis { get; set; }
        public decimal KalanStok { get; set; }
        public decimal ToplamGirisTutar { get; set; }
        public decimal ToplamCikisTutar { get; set; }
        public decimal TutarFarki { get; set; }
        public string? GirisAmbar { get; set; }
        public string? CikisAmbar { get; set; }
        public string? CikisSatisElemani { get; set; }
    }
}