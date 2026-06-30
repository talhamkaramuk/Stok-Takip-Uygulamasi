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
- Sipariş fulfillment takibi: bekleyen, kısmi sevk, sevk edildi ve iptal durumları; kalem bazlı sevk/iade sayaçları
- Alım talep yönetimi: tedarikçi, onay ve teslim alma akışı
- Sevkiyat yönetimi: siparişe bağlı veya bağımsız sevkiyat oluşturma
- İade yönetimi: müşteri iadelerini depoya geri alma
- Operasyon ekranları: arama, durum etiketi, hızlı oluşturma ve tablo üzerinden takip

Stok etkisi bilinçli olarak iş sürecine bağlıdır. Sipariş ve alım talebi oluşturmak stok miktarını değiştirmez. Alım talebi teslim alındığında stok artar. Sevkiyat oluşturulduğunda stok azalır. İade oluşturulduğunda stok tekrar artar.

Alim talebi teslim alma akisi artik tek seferlik tam teslimle sinirli degildir. Onaylanan talepler kismi teslim alinabilir; sistem kalem bazinda `receivedQuantity` tutar ve kalan miktari asan teslimi reddeder.

Sayim akisi MVP'de snapshot model kullanir. Sayim basladiktan sonra ayni depoda herhangi bir stok hareketi olursa arayuz uyari gosterir; farklar yine sayim baslangic snapshot'ina gore yorumlanir.

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

Web arayüzü `http://localhost:5173`, API `http://localhost:8080`, Swagger `http://localhost:8080/swagger` üzerinden açılır. Versiyonlu API yolu `/api/v1` altındadır. Legacy `/api` yolları geçici uyumluluk için açıktır ve `Deprecation`/`Sunset` header'ları döndürür. Docker profili ilk geliştirme akışı için `Database__EnsureCreated=true`, `Database__ApplyDevelopmentSchemaPatches=true` ve `Database__SeedDevelopmentData=true` kullanır.

Yerel çalıştırma:

```powershell
docker compose up -d postgres
dotnet run --project src/STOKIO.Api
cd frontend
npm install
npm run dev
```

Frontend varsayılan olarak `http://localhost:5248` API adresine gider. Farklı adres için `frontend/.env` içinde `VITE_API_BASE_URL` tanımlayın.

Güvenlik notu: Frontend access token'ı `localStorage` veya `sessionStorage` içinde saklamaz; token yalnızca memory state içinde tutulur. Login/register sonrası API HttpOnly refresh cookie yazar, frontend `/api/v1/auth/refresh` ile kısa ömürlü access token yeniler. Refresh cookie production ortamında `HttpOnly`, `Secure`, `SameSite=Lax` ve rotation modeliyle çalışır. Refresh/logout istekleri `X-STOKIO-Refresh: 1` header'ı gerektirir; frontend CSP meta policy ile script/connect yüzeyini sınırlar. Demo giriş bilgileri yalnızca Vite development modunda veya `VITE_ENABLE_DEMO_CREDENTIALS=true` tanımlandığında formda hazır gelir. Varsayılan access token süresi 15 dakikadır; Docker profilinde `STOKIO_ACCESS_TOKEN_MINUTES` ile değiştirilebilir. Varsayılan refresh token süresi 14 gündür; Docker profilinde `STOKIO_REFRESH_TOKEN_DAYS` ile değiştirilebilir.

## Testler

```powershell
dotnet test
cd frontend
npm run build
```

Test katmanlari:

- Unit/service tests: validator, pure domain rule, number generation, idempotency state ve servis davranislarini hizli InMemory kosuda dogrular. Varsayilan `dotnet test` bunlari calistirir.
- Relational integration tests: PostgreSQL unique constraint, check constraint, tenant isolation, concurrency token ve atomic idempotency reservation davranislarini dogrular. Bu testler varsayilan olarak atlanir.
- E2E smoke tests: calisan API uzerinden register/login, product create, stock in/out, count close, shipment, return ve export job akisini dener. Bu testler varsayilan olarak atlanir.

PostgreSQL relational testleri calistirma:

```powershell
docker compose up -d postgres
docker compose exec -T postgres psql -U stokio -d postgres -c "CREATE DATABASE stokio_test;"
$env:STOKIO_TEST_POSTGRES_CONNECTION_STRING="Host=localhost;Port=5432;Database=stokio_test;Username=stokio;Password=stokio_dev_password"
dotnet test --filter "Layer=RelationalIntegration"
```

Relational testler hedef database'i sifirlar. Guvenlik icin connection string icindeki database adinda `test` gecmesi zorunludur.

Bilgisayarda baska bir PostgreSQL zaten `5432` portunu kullaniyorsa test portunu ayirin:

```powershell
$env:POSTGRES_PORT="15432"
docker compose up -d --force-recreate postgres
docker compose exec -T postgres psql -U stokio -d postgres -c "CREATE DATABASE stokio_test;"
$env:STOKIO_TEST_POSTGRES_CONNECTION_STRING="Host=localhost;Port=15432;Database=stokio_test;Username=stokio;Password=stokio_dev_password"
dotnet test --filter "Layer=RelationalIntegration"
```

Calisan API'ye karsi E2E smoke testi:

```powershell
$env:STOKIO_E2E_BASE_URL="http://localhost:8080"
dotnet test --filter "Layer=E2ESmoke"
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

## Export Job Operasyonu

- Arka plan export job'lari Postgres uzerinden atomik claim edilir; coklu API replica ayni job'i ayni anda isleyemez.
- `Exports:JobLockTimeoutSeconds`, beklenen en uzun export suresinden buyuk tutulmalidir.
- Basarisiz export job'lari `RetryBackoffBaseSeconds` ile baslayan exponential backoff uygular, `RetryBackoffMaxSeconds` ile sinirlanir ve varsayilan olarak en fazla 3 kez denenir.
- Worker, `CleanupIntervalMinutes` araliginda expired export dosyalarini siler; `CompletedRetentionDays` suresini asan `Ready`/`Failed` job satirlari DB'den kaldirilir.
- `Exports:StoragePath` development ve tek-node kurulumlarda local filesystem icindir. Scale-out uretimde bu path tum replica'lar tarafindan paylasilan kalici storage olmalidir; object storage/signed URL entegrasyonu ayri adapter olarak eklenmelidir.
