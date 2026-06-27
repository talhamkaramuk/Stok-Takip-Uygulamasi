# Güvenlik

## Kimlik Doğrulama

API JWT Bearer authentication kullanır. Token içinde kullanıcı, rol, tenant id ve tenant slug claim'leri bulunur.

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

API global fixed-window rate limit uygular. Kimliği doğrulanmış kullanıcılar tenant id üzerinden, anonim kullanıcılar IP üzerinden gruplanır.

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
