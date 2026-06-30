# Güvenlik

## Kimlik Doğrulama

API JWT Bearer authentication kullanır. Token içinde kullanıcı, rol, tenant id ve tenant slug claim'leri bulunur.

Access token kısa ömürlüdür ve frontend tarafından kalıcı tarayıcı storage alanına yazılmaz. React uygulaması access token'ı yalnızca memory state içinde tutar; sayfa yenileme veya token süresi yaklaşınca `/api/v1/auth/refresh` çağrısı ile yeni access token alır.

Refresh token sadece API tarafından yazılan HttpOnly cookie içinde tutulur. Cookie production ortamında `HttpOnly`, `Secure`, `SameSite=Lax`, `Path=/api` ve süreli `Expires` ayarlarıyla döner. Refresh token veritabanında düz metin saklanmaz; SHA-256 hash olarak tutulur. Her başarılı refresh eski token'ı `rotated` olarak revoke eder ve yeni refresh token üretir. Logout mevcut refresh token'ı `logout` sebebiyle revoke eder.

Refresh ve logout endpointleri cookie tabanlı olduğu için CSRF'e karşı `X-STOKIO-Refresh: 1` özel header'ını zorunlu tutar. Browser form post'ları bu header'ı üretemez; CORS credentials sadece allowlist'teki frontend origin'lerine açıktır. Frontend CSP policy `default-src 'self'`, `script-src 'self'`, `object-src 'none'` ve sınırlı `connect-src` ile XSS etki alanını daraltır.

Rol politikaları:

- `Owner`
- `Manager`
- `Staff`

Katalog yönetimi `Owner` ve `Manager` rolleriyle sınırlandırılmıştır.
Kullanıcı yönetimi sadece `Owner` rolüyle sınırlandırılmıştır.

## Şifre Saklama

Şifreler düz metin saklanmaz. `Pbkdf2PasswordHasher` PBKDF2-SHA256, rastgele salt ve sabit zamanlı karşılaştırma kullanır.

## Tenant İzolasyonu

Tenant kapsamlı entity'ler `ITenantScoped` uygular. EF Core global query filter her request'te sadece claim içindeki tenant id'ye ait kayıtları döndürür. SaveChanges aşamasında tenant dışı yazma girişimi engellenir.

## Rate Limiting

API global tenant/IP bazlı güvenlik ağına ek olarak endpoint türüne göre ayrı rate limit profilleri uygular:

- Login: IP + tenant slug + e-posta kombinasyonuna göre sıkı fixed-window limit ve ek IP limiti
- Tenant kaydı: IP bazlı daha sıkı fixed-window limit
- Barkod/sayım scan: tenant + kullanıcı bazlı token bucket burst kontrolü
- Export: tenant + kullanıcı bazlı düşük fixed-window limit
- Raporlar: tenant bazlı ayrı fixed-window limit
- Genel okuma endpointleri: tenant bazlı daha geniş fixed-window limit

Rate limiter authentication ve tenant context oluşturulduktan sonra çalışır; bu nedenle authenticated endpointlerde partition key tenant claim'lerinden üretilir.

## Audit Log

Ürün, kategori, kullanıcı, stok ve sayım işlemlerinde audit kaydı oluşturulur. Audit kaydı tenant id, kullanıcı id, aksiyon, entity, metadata ve mümkün olduğunda eski/yeni değer snapshot'ı içerir.

## Stok Bütünlüğü

Kritik stok yazma işlemleri transaction içinde yürütülür. `Product` ve `WarehouseStock` kayıtlarında uygulama yönetimli `Version` concurrency token'ı kullanılır. Çakışan güncellemeler `stock_concurrency_conflict` problemiyle reddedilir.

Tekrar denenen kritik işlemler için `Idempotency-Key` header'ı desteklenir. Aynı key aynı payload ile tekrar geldiğinde sistem önceki sonucu döndürür; aynı key farklı payload ile kullanılırsa işlem reddedilir.

## Export Güvenliği

Excel export endpointleri kimlik doğrulama gerektirir ve veriyi servis katmanındaki tenant filtreli sorgulardan üretir. Client'tan gelen tenant id kullanılmaz.

## Üretim Kontrolleri

- `Jwt__SigningKey` secret manager veya platform secret store üzerinden verilmelidir.
- `Database__EnsureCreated`, `Database__ApplyDevelopmentSchemaPatches` ve `Database__SeedDevelopmentData` üretimde kapalı olmalıdır. Uygulama bu ayarlar açıkken Development dışındaki ortamlarda başlamayı reddeder.
- EF Core migration süreci CI/CD içinde çalıştırılmalıdır.
- PostgreSQL yedekleri şifreli saklanmalı ve restore testi yapılmalıdır.
- API sadece HTTPS üzerinden yayınlanmalıdır.
- CORS allowlist gerçek frontend origin değerleriyle sınırlandırılmalıdır.
- Development dışındaki ortamlarda wildcard CORS ve varsayılan geliştirme JWT anahtarı startup sırasında engellenir.
- Refresh token süresi `Auth:RefreshTokenDays` ile yönetilir ve production secret/config kaynağından ortam bazlı verilmelidir.
