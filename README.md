# STOKIO

STOKIO, küçük işletmeler için barkod destekli stok takip ve sayım sistemidir. Bu repo ASP.NET Core 9 Web API, React, TypeScript, PostgreSQL, JWT, EF Core, FluentValidation, Serilog ve Swagger ile hazırlanmış MVP başlangıç uygulamasını içerir.

## Modüller

- Kullanıcı ve tenant kaydı
- İşletme içi kullanıcı yönetimi
- JWT tabanlı giriş
- Ürün, kategori ve barkod yönetimi
- Stok giriş, çıkış ve düzeltme hareketleri
- Sayım düzeltmeleri için ayrı `CountCorrection` hareket tipi
- Kritik stok listesi
- Sayım başlatma, barkodla sayma, fark hesaplama
- Sayım farklarını stok düzeltmesi olarak uygulama
- Temel stok ve hareket raporları
- Excel dışa aktarma
- Stok defteri tutarlılık kontrolü
- Çoklu depo/lokasyon yönetimi
- Depo bazlı stok bakiyeleri
- Depolar arası stok transferi

## Operasyon Modülleri

Son eklenen operasyon kapsamında STOKIO artık sadece stok defteri değil, satış ve tedarik süreçlerini de takip eder:

- Müşteri yönetimi: satış, sevkiyat ve iade işlemlerinde kullanılan müşteri kartları
- Tedarikçi yönetimi: alım talep sürecinde kullanılan tedarikçi kartları
- Sipariş yönetimi: müşteri, depo, kalem ve durum takibi
- Alım talep yönetimi: tedarikçi, onay ve teslim alma akışı
- Sevkiyat yönetimi: siparişe bağlı veya bağımsız sevkiyat oluşturma
- İade yönetimi: müşteri iadelerini depoya geri alma
- Operasyon ekranları: arama, durum etiketi, hızlı oluşturma ve tablo üzerinden takip

Stok etkisi bilinçli olarak iş sürecine bağlıdır. Sipariş ve alım talebi oluşturmak stok miktarını değiştirmez. Alım talebi teslim alındığında stok artar. Sevkiyat oluşturulduğunda stok azalır. İade oluşturulduğunda stok tekrar artar.

## Mimari

```text
Presentation Layer
  src/STOKIO.Api
  frontend
Application Layer
  src/STOKIO.Application
Domain Layer
  src/STOKIO.Domain
Infrastructure Layer
  src/STOKIO.Infrastructure
PostgreSQL Database
```

## Geliştirme Kurulumu

Gereksinimler:

- .NET 9 SDK
- Node.js 22 veya uyumlu güncel sürüm
- Docker Desktop veya yerel PostgreSQL

Docker ile çalıştırma:

```powershell
Copy-Item .env.example .env
docker compose up --build
```

Web arayüzü `http://localhost:5173`, API `http://localhost:8080`, Swagger `http://localhost:8080/swagger` üzerinden açılır. Versiyonlu API yolu `/api/v1` altındadır. Docker profili ilk geliştirme akışı için `Database__EnsureCreated=true`, `Database__ApplyDevelopmentSchemaPatches=true` ve `Database__SeedDevelopmentData=true` kullanır.

Yerel çalıştırma:

```powershell
docker compose up -d postgres
dotnet run --project src/STOKIO.Api
cd frontend
npm install
npm run dev
```

Frontend varsayılan olarak `http://localhost:5248` API adresine gider. Farklı adres için `frontend/.env` içinde `VITE_API_BASE_URL` tanımlayın.

Güvenlik notu: Frontend access token'ı kalıcı tarayıcı storage alanında saklamaz; oturum yenileme sonrası kullanıcı tekrar giriş yapar. Demo giriş bilgileri yalnızca Vite development modunda veya `VITE_ENABLE_DEMO_CREDENTIALS=true` tanımlandığında formda hazır gelir. Varsayılan access token süresi 15 dakikadır; Docker profilinde `STOKIO_ACCESS_TOKEN_MINUTES` ile değiştirilebilir.

## Testler

```powershell
dotnet test
cd frontend
npm run build
```

## EF Core Migration

Migration aracı repo içindeki local tool manifest ile sabitlenmiştir:

```powershell
dotnet tool restore
dotnet ef migrations add <MigrationName> --project src/STOKIO.Infrastructure/STOKIO.Infrastructure.csproj --startup-project src/STOKIO.Api/STOKIO.Api.csproj --output-dir Persistence/Migrations
dotnet ef database update --project src/STOKIO.Infrastructure/STOKIO.Infrastructure.csproj --startup-project src/STOKIO.Api/STOKIO.Api.csproj
```

İlk migration `src/STOKIO.Infrastructure/Persistence/Migrations` altında `InitialCreate` olarak oluşturulmuştur. Üretimde `EnsureCreated` ve development schema patch kapalı tutulmalı, şema değişiklikleri bu migration akışıyla uygulanmalıdır.

## Üretim Notları

- `Jwt__SigningKey` en az 32 byte, rastgele ve ortam değişkeninden gelmelidir.
- Üretimde `Database__EnsureCreated=false`, `Database__ApplyDevelopmentSchemaPatches=false` ve `Database__SeedDevelopmentData=false` olmalıdır; EF Core migration kullanılmalıdır.
- HTTPS ters proxy veya platform seviyesinde zorunlu hale getirilmelidir.
- PostgreSQL yedekleri günlük çalıştırılmalı ve düzenli restore testi yapılmalıdır.
- CORS izinleri sadece gerçek frontend origin değerleriyle sınırlandırılmalıdır.
- Liveness endpoint'i `/health`, readiness endpoint'i `/health/ready` adresindedir.
- Kritik stok yazma endpointlerinde tekrar denemeler için `Idempotency-Key` header'ı kullanılabilir.

Ek ayrıntılar için:

- [Mimari](docs/ARCHITECTURE.md)
- [API](docs/API.md)
- [Güvenlik](docs/SECURITY.md)
- [Üretim Hazırlığı](docs/PRODUCTION_READINESS.md)
