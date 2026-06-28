# Mimari

STOKIO katmanlı mimariyle ayrıştırılmıştır.

## Domain

`src/STOKIO.Domain` dış bağımlılık içermez. Tenant, kullanıcı, kategori, ürün, barkod, depo, depo stok bakiyesi, stok hareketi, sayım ve audit entity'leri burada yer alır.

## Application

`src/STOKIO.Application` DTO, servis sözleşmeleri, validasyon kuralları ve uygulama hata tiplerini içerir. Bu katman HTTP veya EF Core ayrıntılarına bağımlı değildir.

## Infrastructure

`src/STOKIO.Infrastructure` EF Core DbContext, PostgreSQL konfigürasyonu, JWT üretimi, PBKDF2 şifre hashleme ve iş servislerini içerir.

Tenant izolasyonu iki seviyede uygulanır:

- JWT içindeki `tenant_id` claim'i request scope içinde `ICurrentTenant` olarak ayarlanır.
- EF Core global query filter tenant kapsamlı tablolarda sadece ilgili tenant verisini döndürür.
- Export, rapor ve stok tutarlılık kontrolleri de aynı servisleri kullandığı için tenant filtresinden geçer.

Stok modeli iki seviyelidir:

- `StockMovement` append-only stok defteridir ve uzun vadede authoritative kayıt kaynağıdır.
- `WarehouseStock` depo/lokasyon bazlı stok bakiyesi projection'ıdır.
- `Product.CurrentStock` toplam stok bakiyesini geriye uyumlu ve hızlı okuma için tutan projection alanıdır.
- Normal stok hareketleri ilgili depo bakiyesini ve toplam ürün stok bakiyesini birlikte günceller.
- Depolar arası transfer toplam ürün stokunu değiştirmez; kaynak depoda `TransferOut`, hedef depoda `TransferIn` defter satırı oluşturur.
- Sayımlar depo bazlıdır; fark uygulama yalnızca seçili depo bakiyesini düzeltir.

Stok yazma stratejisi:

- Manuel stok hareketi, depo transferi, alım teslim alma, sevkiyat, iade ve sayım kapatma açık transaction içinde çalışır.
- Stok yazan servisler etkilenen ürün ve depo stok satırlarını deterministik sırayla hazırlar.
- PostgreSQL üzerinde `Products` ve `WarehouseStocks` satırlarına `FOR UPDATE` row lock alınır; lock alındıktan sonra tracked entity değerleri yeniden yüklenir.
- PostgreSQL dışı providerlarda `Product.Version` ve `WarehouseStock.Version` concurrency token'ları çakışma yakalama için kullanılır.
- Geçmiş `StockMovement` satırları değiştirilmez; düzeltmeler yeni hareket olarak append edilir.

Operasyon modeli stok defterinin ustune is sureci katmani ekler:

- `Customer` satis, sevkiyat ve iade akislari icin tenant bazli musteri kartlarini tutar.
- `Supplier` alim talep akisi icin tenant bazli tedarikci kartlarini tutar.
- `SalesOrder` musteri siparislerini ve sevkiyat durumunu izler; tek basina stok degistirmez.
- `SalesOrderItem` ordered quantity, shipped quantity ve returned quantity sayaclarini tutar. `shipped <= ordered` ve `returned <= shipped` kurallari hem servis validasyonu hem veritabani constraint'leri ile korunur.
- `PurchaseRequest` tedarik talebini, onayi ve teslim alma adimini izler; stok artisi yalnizca teslim alma adiminda olusur.
- `Shipment` siparise bagli veya bagimsiz sevkiyatlari tutar; stok dusumu bu adimda yapilir. Siparise bagli sevkiyat fazla sevkiyati engeller ve siparisi `PartiallyShipped` veya `Shipped` durumuna tasir.
- `ReturnRequest` musteri iadelerini kaydeder; iade edilen miktar ilgili depoya geri girer. Siparise bagli iadelerde miktar, daha once sevk edilmis ve henuz iade edilmemis bakiye ile sinirlanir.

## Presentation

`src/STOKIO.Api` minimal API endpoint'lerini, JWT doğrulamayı, role-based authorization policy'lerini, rate limiting, CORS, Swagger ve merkezi hata formatını içerir.

Yeni sözleşmeler `/api/v1` altındadır. Geçiş uyumluluğu için `/api` yolları da map edilir.

`frontend` React ve TypeScript ile hazırlanmış operasyonel SPA arayüzüdür.
