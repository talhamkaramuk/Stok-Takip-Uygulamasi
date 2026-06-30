# Üretim Hazırlığı ve Büyüme Yol Haritası

Bu doküman STOKIO'nun demo/pilot seviyesinden gerçek müşteri verisiyle çalışabilecek üretim seviyesine taşınması için uygulanacak teknik sözleşmeleri ve öncelikleri tanımlar.

## Genel Sıralama

1. Veri kaybını önle.
2. Güvenliği sertleştir.
3. Gözlemlenebilirliği ekle.
4. Test ve release sürecini otomatikleştir.
5. Ölçeklenebilirlik ve kurumsal kullanım kabiliyetlerini geliştir.

## P0 - Üretim Bloklayıcıları

- `Database:EnsureCreated`, `Database:ApplyDevelopmentSchemaPatches` ve `Database:SeedDevelopmentData` yalnızca Development ortamında açık olabilir.
- Üretim şema değişiklikleri EF Core migration süreciyle yapılmalıdır.
- JWT imza anahtarı, veritabanı parolası ve connection string dış secret kaynağından gelmelidir.
- Swagger üretimde herkese açık olmamalıdır.
- CORS sadece gerçek frontend origin değerleriyle sınırlandırılmalıdır.
- HTTPS platform veya ters proxy seviyesinde zorunlu olmalıdır.
- Browser access token'ı kalıcı storage alanına yazılmamalı; access token memory-only, refresh token HttpOnly/Secure/SameSite cookie ve rotation modeliyle çalışmalıdır.
- Cookie tabanlı refresh/logout akışı CSRF stratejisiyle korunmalı ve frontend CSP production domainlerine göre sıkı tutulmalıdır.
- Demo kullanıcı ve demo veriler üretimde oluşturulmamalıdır.
- PostgreSQL için otomatik yedek ve düzenli restore testi yapılmalıdır.
- Export job worker'lari yalnizca veritabanindan atomik claim ile is almalidir; process-ici queue uretimde kullanilamaz.
- Export dosyalari scale-out uretimde local container filesystem'e yazilmamalidir. `Exports:StoragePath` paylasimli kalici volume olmalidir veya object storage adapter'i tenant-scoped key ve signed URL ile devreye alinmalidir.

Kabul kriterleri:

- Üretimde uygulama geliştirme seed veya `EnsureCreated` açıkken başlamaz.
- Üretimde varsayılan geliştirme JWT anahtarı kullanılamaz.
- Şema değişiklikleri migration dışında uygulanmaz.
- Restore prosedürü dokümante edilmiş ve test edilmiştir.

Mevcut durum:

- `InitialCreate` EF Core migration dosyası oluşturuldu.
- `dotnet-ef` local tool manifesti `.config/dotnet-tools.json` altında sabitlendi.
- Development hızlı başlangıç akışı için schema patch korunur; üretimde kapalı kalır.
- Legacy `/api` route'ları geçici olarak korunur ve `Deprecation`/`Sunset` header'ları döndürür. Yeni istemciler `/api/v1` kullanmalı; 31 Aralık 2026 sonrası release'te `/api` route'ları kaldırılmalıdır.
- Export job claim akisi `ExportJobs.LockedBy`, `LockedUntil`, `RetryCount`, `MaxRetryCount`, `LastAttemptAt` ve `NextAttemptAt` alanlariyla Postgres uzerinden yapilir. Stale `Processing` job'lar lock timeout sonrasinda farkli worker tarafindan reclaim edilebilir.
- Basarisiz export job'lari exponential backoff ile yeniden denenir; terminal hatalarda `FailedReasonCode` yazilir. Worker expired dosyalari siler ve retention disindaki terminal job satirlarini kaldirir.
- Access token frontend memory state içinde tutulur; refresh token düz metin saklanmaz, HttpOnly cookie üzerinden taşınır ve her refresh işleminde rotate edilir. Refresh/logout endpointleri `X-STOKIO-Refresh: 1` header'ını zorunlu tutar.

## P1 - Stok Veri Bütünlüğü

Stok etkileyen tüm use-case'ler açık transaction sınırına sahip olmalıdır:

- Alım talebi teslim alma
- Sevkiyat oluşturma
- İade oluşturma
- Manuel stok girişi/çıkışı/düzeltmesi
- Sayım kapatma ve fark uygulama
- Depolar arası transfer

Stok operasyonu sözleşmesi:

```text
Komut
  - tenant id, user id, operation type, warehouse id, product id, quantity, reason, request id alır
  - tenant claim doğrulanır
  - rol ve entity durumu doğrulanır
  - quantity > 0 kontrol edilir
  - ürün ve depo aktif olmalıdır
  - çıkış işlemlerinde yeterli stok aranır
  - kritik işlemler request id ile idempotent olmalıdır
  - transaction başlatılır
  - etkilenen ürün ve depo stok satırları deterministik sırayla hazırlanır
  - PostgreSQL üzerinde etkilenen satırlara `FOR UPDATE` row lock alınır
  - PostgreSQL dışı providerlarda version-check ile concurrency çakışması yakalanır
  - warehouse stock ve ürün toplam stoku güncellenir
  - stock movement append edilir
  - audit log append edilir
  - commit edilir
```

Hedef stok defteri kuralı: `StockMovement` append-only authoritative ledger olmalı, düzeltmeler yeni hareket olarak yazılmalı, geçmiş hareketler değiştirilmemelidir. `WarehouseStock` ve `Product.CurrentStock` ledger'dan türeyen balance projection olarak ele alınmalıdır.

Mevcut durum:

- Manuel stok hareketi, alım teslim alma, sevkiyat, iade, sayım kapatma ve depo transferi transaction içinde çalışır.
- Çok satırlı stok operasyonları ürün ve depo stok satırlarını deterministik sırayla işler.
- PostgreSQL'de stok yazma sırasında `Products` ve `WarehouseStocks` satırları explicit row lock ile kilitlenir.
- `Product.Version` ve `WarehouseStock.Version` concurrency token olarak kullanılır.
- Sayim kapatma MVP'de snapshot model uygular. Sayim baslangicindan sonra ayni depoda herhangi bir stok hareketi olursa API/UI uyari verir; hareketler delta-aware sekilde otomatik fark hesabina katilmaz.
- Alim talebi teslim alma akislari `PurchaseRequestItem.ReceivedQuantity` ve `Version` alanlari uzerinden takip edilir. Kismi teslim alma stok hareketini yalnizca teslim alinan miktar kadar yazar; kalan miktari asan teslim alma servis validasyonu ve check constraint'leri ile reddedilir.
- Siparise bagli sevkiyat ve iade akislari `SalesOrderItem.ShippedQuantity`, `ReturnedQuantity` ve `Version` alanlari uzerinden takip edilir. Fazla sevkiyat ve sevk edilenden fazla iade servis validasyonu ve check constraint'leri ile reddedilir.
- Kritik stok yazma işlemleri `Idempotency-Key` header'ını destekler.
- Concurrency çakışmaları `stock_concurrency_conflict` koduyla 409 olarak döner.

## P2 - Güvenlik Sertleştirme

- Endpoint bazlı rate limit profilleri uygulanır: login, tenant kaydı, export, barcode/scan, rapor ve genel okuma endpointleri ayrı limitlenir.
- Tenant kaydı akışına yüksek riskli deploymentlarda CAPTCHA veya benzeri abuse-prevention katmanı eklenmelidir.
- Export işlemleri büyüdükçe düşük limit korunmalı ve arka plan export queue tasarımına taşınmalıdır.
- JWT anahtar rotasyon prosedürü tanımlanmalıdır.
- Audit log görüntüleme ekranı eklenmelidir.
- Tenant izolasyonu testleri CI içinde zorunlu koşul olmalıdır.
- Hassas hata detayları response içine yazılmamalıdır.

Minimum üretim kapısı:

- Repo veya config içinde secret yok.
- Üretim demo kullanıcısı yok.
- Wildcard CORS yok.
- Swagger kapalı veya korumalı.
- Güçlü dış JWT anahtarı var.
- Kritik operasyonlarda audit zorunlu.

## P3 - Gözlemlenebilirlik ve Operasyon

Eklenmesi gereken sinyaller:

- Correlation id response header ve loglarda görünmelidir.
- Readiness health check veritabanı bağlantısını kontrol etmelidir.
- Metrikler: request count, latency, 4xx/5xx, login success/failure, tenant registration, stock movement count, failed stock out, export duration/failure, database latency/failure, tenant activity.
- Kritik hata ve health failure için uyarı mekanizması kurulmalıdır.

Mevcut durum:

- `/health` liveness endpoint'i vardır.
- `/health/ready` veritabanı bağlantısını kontrol eden readiness endpoint'i olarak eklenmiştir.
- `IMetricsRecorder` production ortamında process içi sayaç tutmaz; `STOKIO.Api` OpenTelemetry meter'i üzerinden OTLP exporter'a yazar.
- Uygulama request count/latency, 4xx/5xx count, login success/failure, stock movement count, export success/failure ve DB readiness latency sinyallerini dış collector'a aktarır.
- Production ortamında `Observability:Metrics:OtlpEndpoint` veya `OTEL_EXPORTER_OTLP_ENDPOINT` zorunludur; eksikse uygulama fail-fast başlar.
- `/api/v1/observability/metrics` JSON snapshot endpoint'i debug/development amaçlıdır ve sadece `Observability:Metrics:EnableDebugSnapshotEndpoint=true` iken map edilir.

## P4 - Test ve Release Kalitesi

CI pipeline hedefi:

- Backend testleri
- Frontend build
- Statik analiz
- Docker image build
- Migration testleri
- E2E smoke testleri

E2E kapsamı:

- Register/login
- Ürün ve barkod oluşturma
- Manuel stok giriş/çıkış
- Yetersiz stok reddi
- Sayım başlatma/kapatma
- Sevkiyat
- İade
- Excel export
- Cross-tenant erişim engeli

Release sözleşmesi:

```text
Deploy öncesi
  - DB yedeği al
  - migration'ı staging ortamında doğrula
  - destructive change kontrolü yap
  - rollback planını doğrula

Deploy sırasında
  - güvenli migration penceresi kullan
  - migration'ı uygula
  - smoke testleri çalıştır

Deploy sonrası
  - health endpointlerini doğrula
  - login, stok tutarlılığı ve kritik raporları kontrol et
```

## P5 - Performans ve Ölçeklenebilirlik

- Büyük listelerde frontend tarafında yüksek `pageSize` yerine server-side pagination/filtering zorunlu hale getirilmelidir.
- Büyük Excel export işlemleri limitlenmeli veya arka plan işine taşınmalıdır.
- `StockMovements` için tenant/product/warehouse/date indeksleri gözden geçirilmelidir.
- Dashboard ve rapor ekranları için okuma modeli veya cache stratejisi hazırlanmalıdır.
- Stock movement ve audit log için arşivleme politikası belirlenmelidir.

## P6 - Orta ve Büyük İşletme Kabiliyetleri

- Gelişmiş yetki matrisi
- Depo -> bölüm -> raf -> göz hiyerarşisi
- Offline/PWA sayım modu
- Çok dil altyapısı
- ERP, e-fatura ve muhasebe entegrasyonları
- Çoklu depo operasyonlarında transfer onay akışı
- Gelişmiş raporlama ve maliyet takibi

## Üretim Config Sözleşmesi

```text
Database:EnsureCreated=false
Database:ApplyDevelopmentSchemaPatches=false
Database:SeedDevelopmentData=false
HTTPS required=true
Swagger public=false
Cors:AllowedOrigins=explicit frontend domains
Jwt:SigningKey=external secret
ConnectionStrings:DefaultConnection=external secret
Structured logging=enabled
Health endpoints=enabled
Observability:Metrics:OtlpEndpoint=configured collector URI
Observability:Metrics:EnableDebugSnapshotEndpoint=false
```
