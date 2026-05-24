
# 📄 EBelge Entegrasyon

![License](https://img.shields.io/github/license/dogukankosan/EBelgeEntegrasyonu)
![Stars](https://img.shields.io/github/stars/dogukankosan/EBelgeEntegrasyonu)
![Issues](https://img.shields.io/github/issues/dogukankosan/EBelgeEntegrasyonu)
![Last Commit](https://img.shields.io/github/last-commit/dogukankosan/EBelgeEntegrasyonu)

<img width="1600" height="739" alt="9ab4e47a-c6d5-4752-8a1d-741a701b1743" src="https://github.com/user-attachments/assets/eb75e6dc-cb6e-45ff-b610-f7bacf2dc954" />


> **EBelge Entegrasyon**, e-Logo üzerinden kesilen e-Arşiv ve e-Fatura belgelerini otomatik olarak Logo Bulut ERP'ye aktaran, ASP.NET Core tabanlı bir ara katman entegrasyon sistemidir.

---

## 🚀 Özellikler

- 📥 e-Logo SOAP API üzerinden e-Arşiv ve e-Fatura listesi çekme
- 📄 UBL formatında fatura detayı ve görsel (HTML/PDF) görüntüleme
- 🚀 Tek ve toplu fatura transferi (Logo Bulut ERP REST API)
- 🏭 Ambar ve satış elemanı yönetimi
- 🚫 Cari kara liste filtreleme
- 🔐 JWT tabanlı kimlik doğrulama (Logo ERP kullanıcısıyla)
- 🔒 AES-256 şifreli ayar depolama
- 📊 Dashboard — transfer istatistikleri, son hatalı/başarılı transferler
- 📝 API log sistemi (INFO / WARNING / ERROR)
- ⚙️ Admin panel — e-Logo ve Logo ERP ayarları yönetimi
- 🪟 Windows Service desteği
- 🔍 Lot/Seri No takip raporu (Logo ERP SQL sorgusu, sayfalı çekim, 30 dk önbellek)

---

## 🗂 Proje Yapısı

```
EBelgeEntegrasyonu/
├── EBelgeAPI/                  # ASP.NET Core Web API
│   ├── Controllers/
│   │   ├── ...                 # Mevcut controller'lar
│   │   └── LotTakipController.cs   # Lot/Seri No takip raporu endpoint'leri
│   ├── Data/                   # Repository'ler ve arayüzler
│   ├── Middleware/             # ApiKey, JWT revocation, exception middleware
│   ├── Models/                 # Entity, DTO, Request, Response modelleri
│   ├── Services/
│   │   ├── ...                 # Mevcut servisler
│   │   └── LotTakipService.cs  # Sayfalı SQL sorgusu, cache yönetimi, DTO mapping
│   └── Program.cs              # Uygulama başlangıç noktası
│
└── EBelgeUI/                   # ASP.NET Core MVC (Razor Views)
    ├── Controllers/
    │   ├── ...                 # Mevcut controller'lar
    │   └── LotTakipController.cs   # Lot takip UI controller (API proxy + cache sıfırlama)
    ├── Filters/                # Session auth filter
    ├── Middleware/             # Exception middleware
    ├── Models/                 # ViewModel'ler
    ├── Views/                  # Razor view'ları
    └── Program.cs              # Uygulama başlangıç noktası
```

---

## 🛠️ Kurulum & Çalıştırma

### Gereksinimler

- .NET 9.0+
- SQL Server
- Logo Bulut ERP hesabı
- e-Logo hesabı

### 1. Projeyi Klonla

```bash
git clone https://github.com/dogukankosan/EBelgeEntegrasyonu.git
cd EBelgeEntegrasyonu
```

### 2. Veritabanını Oluştur

SQL Server'da `EBelgeDb` adında bir veritabanı oluştur ve gerekli tabloları migrate et.

### 3. API Ayarlarını Yapılandır

`EBelgeAPI/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=.;Database=EBelgeDb;Trusted_Connection=True;"
  },
  "Jwt": {
    "Secret": "32-karakter-gizli-anahtar",
    "Issuer": "EBelgeAPI",
    "Audience": "EBelgeUI",
    "ExpiresHours": 8
  },
  "ApiKey": "API_ANAHTARINIZ",
  "Encryption": {
    "Key": "32-karakter-sifreleme-anahtari"
  },
  "Settings": {
    "AdminKey": "ADMIN_ANAHTARINIZ"
  },
  "Urls": "http://0.0.0.0:7231"
}
```

### 4. UI Ayarlarını Yapılandır

`EBelgeUI/appsettings.json`:

```json
{
  "ApiSettings": {
    "BaseUrl": "http://localhost:7231",
    "ApiKey": "API_ANAHTARINIZ"
  },
  "Urls": "http://0.0.0.0:7083"
}
```

### 5. Çalıştır

```bash
# API
cd EBelgeAPI
dotnet run

# UI (ayrı terminal)
cd EBelgeUI
dotnet run
```

---

## 🪟 Windows Service Olarak Kurulum

```bash
# API servisi
sc create EBelgeAPI binPath="C:\EBelge\EBelgeAPI\EBelgeAPI.exe" start=auto
sc start EBelgeAPI

# UI servisi
sc create EBelgeUI binPath="C:\EBelge\EBelgeUI\EBelgeUI.exe" start=auto
sc start EBelgeUI
```

---

## ⚡ Kullanım Senaryosu

1. Tarayıcıdan `http://SUNUCU_IP:7083` adresine git.
2. Logo ERP kullanıcı adı ve şifresiyle giriş yap.
3. **Satış Faturaları** menüsünden tarih aralığı seç ve filtrele.
4. Faturayı görüntüle (HTML/PDF), ambar ve satış elemanı seç.
5. Tek fatura veya toplu transfer ile Logo ERP'ye aktar.
6. **Dashboard** üzerinden transfer istatistiklerini takip et.
7. **Kara Liste** ile belirli carileri filtreden çıkar.
8. **Lot Takip** menüsünden seri/lot numarası bazlı giriş-çıkış hareketlerini, stok durumunu ve tutar bilgilerini görüntüle; gerektiğinde önbelleği sıfırla.
9. **Hata Logları** üzerinden sistem hatalarını incele.

---

## 🔌 API Endpoint'leri

| Grup | Endpoint | Açıklama |
|---|---|---|
| Auth | `POST /api/auth/login` | Logo ERP ile giriş |
| Fatura | `GET /api/sales-invoice/list` | Fatura listesi |
| Fatura | `GET /api/sales-invoice/detail/{uuid}` | Fatura detayı |
| Transfer | `POST /api/sales-invoice/transfer/{uuid}` | Tek transfer |
| Transfer | `POST /api/sales-invoice/transfer/toplu` | Toplu transfer |
| Ambar | `GET /api/ambar` | Ambar listesi |
| Dashboard | `GET /api/dashboard/stats` | İstatistikler |
| Lot Takip | `GET /api/reports/lot-takip` | Lot/Seri No takip raporu (cacheli) |
| Lot Takip | `POST /api/reports/lot-takip/cache-sifirla` | Önbelleği sıfırla |
| Ayarlar | `GET /api/settings` | Sistem ayarları (Admin) |

---

## 🤝 Katkı

Katkı sağlamak için projeyi forklayabilir ve pull request gönderebilirsiniz.

---

## 📄 Lisans

MIT License

---

## 📬 İletişim

- 👨‍💻 Geliştirici: [@dogukankosan](https://github.com/dogukankosan)
- 🐞 Suggestions or issues: [Issues sekmesi](https://github.com/dogukankosan/EBelgeEntegrasyonu/issues)

---

<p align="center">
  <img src="https://img.shields.io/badge/.NET-9.0-blue?logo=dotnet" alt="dotnet" />
  <img src="https://img.shields.io/badge/ASP.NET_Core-MVC_&_API-purple" alt="aspnet" />
  <img src="https://img.shields.io/badge/SQL_Server-Database-red?logo=microsoftsqlserver" alt="sqlserver" />
  <img src="https://img.shields.io/badge/Logo_ERP-Entegrasyon-orange" alt="logo" />
  <img src="https://img.shields.io/badge/Windows%20Service-Destekli-lightgrey" alt="windows" />
</p>
