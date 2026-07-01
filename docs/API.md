# API

Varsayılan yerel API adresleri:

- Docker: `http://localhost:8080`
- Yerel `dotnet run`: `http://localhost:5248` ve `https://localhost:7248`

API sözleşmesi `/api/v1` altındadır. Eski `/api` yolları geçiş uyumluluğu için açık tutulur.

Legacy `/api` route'ları geçici uyumluluk yüzeyidir. Bu route'lar response header olarak `Deprecation: true`, `Sunset: Thu, 31 Dec 2026 23:59:59 GMT` ve `Link: </api/v1>; rel="successor-version"` döndürür. Yeni frontend ve yeni entegrasyonlar yalnızca `/api/v1` kullanmalıdır; uygulama `31 Dec 2026 23:59:59 GMT` sonrasında legacy `/api` mapping'lerini startup sırasında map etmez.

Legacy usage takibi için `stokio.legacy_api.requests` metriği yayınlanır. Debug snapshot endpoint'i açıkken tenant owner kullanıcılar `/api/v1/observability/legacy-api-usage` ile sunset öncesi client/route bazlı kullanım raporu alabilir.

Kimlik gerektiren çağrılarda header:

```http
Authorization: Bearer <access_token>
```

Access token response body içinde döner ve kısa ömürlüdür. Browser istemcisi access token'ı kalıcı storage alanına yazmamalıdır. API login/register sonrası `stokio.refresh` adlı HttpOnly refresh cookie set eder. Refresh ve logout çağrıları cookie gönderimi için credentials ve CSRF guard için aşağıdaki header'ı gerektirir:

```http
X-STOKIO-Refresh: 1
```

Kritik stok yazma işlemlerinde istemci tekrar deneme yapacaksa idempotency header'ı gönderilmelidir:

```http
Idempotency-Key: <client-generated-unique-key>
```

Aynı key aynı istek gövdesiyle tekrar kullanıldığında sistem daha önce oluşan sonucu döndürür ve stok ikinci kez değişmez. Aynı key farklı payload ile kullanılırsa `idempotency_key_conflict` hatası döner.

Bu header şu işlemlerde desteklenir:

- `POST /api/v1/stock/movements`
- `POST /api/v1/warehouses/transfers`
- `POST /api/v1/purchase-requests/{id}/receive`
- `POST /api/v1/shipments`
- `POST /api/v1/returns`
- `POST /api/v1/counts/{id}/close`

## Rate Limits

API rate limitleri endpoint türüne göre ayrılmıştır:

| Endpoint türü | Partition | Profil |
| --- | --- | --- |
| `POST /auth/login` | IP + tenant slug + e-posta, ayrıca IP | Sıkı fixed-window |
| `POST /auth/register-tenant` | IP | Daha sıkı fixed-window |
| `POST /counts/{id}/items/scan` | Tenant + kullanıcı | Token bucket, burst kontrollü |
| `/exports/*` | Tenant + kullanıcı | Düşük fixed-window |
| `/reports/*` | Tenant | Ayrı fixed-window |
| Genel okuma endpointleri | Tenant | Daha geniş fixed-window |

## Authentication

`POST /api/v1/auth/register-tenant`

```json
{
  "businessName": "STOKIO Demo",
  "tenantSlug": "stokio-demo",
  "ownerName": "Talha",
  "email": "owner@stokio.local",
  "password": "StrongPass123",
  "taxNumber": null,
  "phone": null
}
```

`POST /api/v1/auth/login`

```json
{
  "tenantSlug": "stokio-demo",
  "email": "owner@stokio.local",
  "password": "StrongPass123"
}
```

Başarılı register/login response'u:

```json
{
  "accessToken": "<jwt>",
  "expiresAt": "2026-06-30T12:15:00+00:00",
  "user": {
    "id": "00000000-0000-0000-0000-000000000000",
    "tenantId": "00000000-0000-0000-0000-000000000000",
    "tenantSlug": "stokio-demo",
    "fullName": "Talha",
    "email": "owner@stokio.local",
    "role": "Owner"
  }
}
```

`POST /api/v1/auth/refresh`

Body gönderilmez. Geçerli refresh cookie ve `X-STOKIO-Refresh: 1` header'ı varsa yeni access token döner ve refresh cookie rotate edilir.

`POST /api/v1/auth/logout`

Body gönderilmez. Geçerli refresh cookie varsa revoke edilir ve cookie temizlenir.

## Products

`GET /api/v1/products`

Query:

```text
search
categoryId
isActive
page
pageSize
```

Response sayfalıdır:

```json
{
  "items": [],
  "page": 1,
  "pageSize": 25,
  "totalCount": 0,
  "totalPages": 0
}
```

`POST /api/v1/products`

```json
{
  "sku": "KBL-001",
  "name": "USB-C Kablo",
  "description": "1 metre",
  "categoryName": "Aksesuar",
  "criticalStockLevel": 5,
  "initialStock": 25,
  "barcodes": ["8690000000010"]
}
```

`PUT /api/v1/products/{id}`

`POST /api/v1/products/{id}/barcodes`

```json
{
  "barcode": "8690000000027",
  "isPrimary": false
}
```

`DELETE /api/v1/products/{id}` ürünü pasife alır.

## Categories

`GET /api/v1/categories`

`POST /api/v1/categories`

```json
{
  "name": "Aksesuar"
}
```

`PUT /api/v1/categories/{id}`

```json
{
  "name": "Telefon Aksesuarı",
  "isActive": true
}
```

`DELETE /api/v1/categories/{id}` kategoriyi pasife alır.

## Users

Bu endpointler `Owner` rolü gerektirir.

`GET /api/v1/users`

`POST /api/v1/users`

```json
{
  "fullName": "Personel",
  "email": "staff@stokio.local",
  "password": "StrongPass123",
  "role": "Staff"
}
```

`PUT /api/v1/users/{id}`

`DELETE /api/v1/users/{id}` kullanıcıyı pasife alır.

## Warehouses

Bu endpointler orta ve büyük işletmeler için çoklu depo/lokasyon stok yönetimini sağlar.

`GET /api/v1/warehouses`

`POST /api/v1/warehouses`

```json
{
  "code": "MAIN",
  "name": "Ana Depo",
  "address": "Merkez",
  "isDefault": true
}
```

`PUT /api/v1/warehouses/{id}`

`DELETE /api/v1/warehouses/{id}` depoyu pasife alır. Varsayılan veya içinde stok olan depo pasife alınamaz.

`GET /api/v1/warehouses/stocks`

Query:

```text
warehouseId
productId
```

`POST /api/v1/warehouses/transfers`

```json
{
  "productId": "00000000-0000-0000-0000-000000000000",
  "fromWarehouseId": "00000000-0000-0000-0000-000000000000",
  "toWarehouseId": "00000000-0000-0000-0000-000000000000",
  "quantity": 5,
  "reason": "Şube replenishment"
}
```

Transfer toplam ürün stok miktarını değiştirmez; kaynak depo bakiyesini azaltır, hedef depo bakiyesini artırır ve iki hareket satırı oluşturur: `TransferOut`, `TransferIn`.

## Stock

`POST /api/v1/stock/movements`

```json
{
  "productId": "00000000-0000-0000-0000-000000000000",
  "warehouseId": null,
  "type": "In",
  "quantity": 10,
  "reason": "Tedarik"
}
```

`type` değerleri:

- `In`: stok artırır
- `Out`: stok azaltır
- `Adjustment`: `quantity` değerini yeni stok miktarı olarak uygular
- `CountCorrection`: sayım kaynaklı düzeltme olarak `quantity` değerini yeni stok miktarı olarak uygular
- `TransferIn` ve `TransferOut`: sadece transfer endpoint'i tarafından oluşturulur

`GET /api/v1/stock/movements`

Query:

```text
productId
warehouseId
type
from
to
page
pageSize
```

`GET /api/v1/stock/critical`

`GET /api/v1/stock/consistency`

Stok yazma işlemleri transaction içinde çalışır. Kritik stok yazma akışlarında etkilenen ürün ve depo stok satırları deterministik sırayla işlenir. PostgreSQL üzerinde bu satırlara explicit `FOR UPDATE` lock alınır; concurrency token'ları ek güvenlik ağı olarak korunur. Eş zamanlı stok güncellemesi yine çakışırsa API `stock_concurrency_conflict` problemiyle 409 döndürür.

## Inventory Counts

`POST /api/v1/counts`

```json
{
  "name": "Haziran Sayımı",
  "warehouseId": null
}
```

`warehouseId` verilmezse varsayılan depo sayılır. Farklar uygulanırken yalnızca ilgili depo bakiyesi düzeltilir.

Sayim MVP'de snapshot model ile calisir. Sayim baslatildiginda aktif urunler icin beklenen miktar snapshot olarak kaydedilir; sayim kapatma fark hesabinda bu snapshot esas alinir. Sayim acikken ayni depoda herhangi bir stok hareketi olursa `InventoryCountDto` su uyari alanlarini dondurur:

- `hasPostSnapshotMovements`
- `postSnapshotMovementCount`
- `lastPostSnapshotMovementAt`

Bu alanlar true/dolu ise UI kullaniciya "sayim baslangicindan sonra stok hareketi olustu" uyarisi gosterir. Sistem bu hareketleri otomatik delta olarak fark hesabina katmaz.

`POST /api/v1/counts/{id}/items/scan`

```json
{
  "barcode": "8690000000010",
  "quantity": 1
}
```

`GET /api/v1/counts/{id}/differences`

`POST /api/v1/counts/{id}/close`

```json
{
  "applyDifferences": true
}
```

## Customers And Suppliers

Cari kart endpointleri satis, sevkiyat, iade ve alim talep formlarinda kullanilan kayitli taraflari yonetir.

`GET /api/v1/customers`

Query:

```text
search
isActive
page
pageSize
```

`POST /api/v1/customers`

```json
{
  "code": "C-001",
  "name": "Techno Market A.S.",
  "contactName": "Ahmet Yilmaz",
  "email": "ahmet@example.com",
  "phone": "05551234567",
  "taxNumber": "1234567890",
  "address": "Istanbul",
  "notes": "Oncelikli musteri"
}
```

`PUT /api/v1/customers/{id}`

```json
{
  "code": "C-001",
  "name": "Techno Market A.S.",
  "contactName": "Ahmet Yilmaz",
  "email": "ahmet@example.com",
  "phone": "05551234567",
  "taxNumber": "1234567890",
  "address": "Istanbul",
  "notes": "Oncelikli musteri",
  "isActive": true
}
```

`DELETE /api/v1/customers/{id}` musteriyi pasife alir.

`GET /api/v1/suppliers`

Query:

```text
search
isActive
page
pageSize
```

`POST /api/v1/suppliers`

```json
{
  "code": "S-001",
  "name": "Telco Tedarik A.S.",
  "contactName": "Zeynep Kaya",
  "email": "tedarik@example.com",
  "phone": "05557654321",
  "taxNumber": null,
  "address": "Ankara",
  "notes": null
}
```

`PUT /api/v1/suppliers/{id}`

`DELETE /api/v1/suppliers/{id}` tedarikciyi pasife alir.

## Operations

Operasyon endpointleri satis, tedarik, sevkiyat ve iade akisini yonetir. Tum operasyonlar tenant izolasyonu ve rol bazli yetkilendirme altindadir.

### Orders

`GET /api/v1/orders`

`POST /api/v1/orders`

```json
{
  "customerId": "00000000-0000-0000-0000-000000000000",
  "customerName": "Techno Market A.S.",
  "warehouseId": "00000000-0000-0000-0000-000000000000",
  "notes": "Magaza teslimi",
  "items": [
    {
      "productId": "00000000-0000-0000-0000-000000000000",
      "quantity": 2
    }
  ]
}
```

Siparis olusturmak stok miktarini degistirmez. Stok dusumu sevkiyat olusturuldugunda yapilir.

Siparis durum modeli:

- `Draft`: ileride taslak akislar icin ayrildi.
- `Pending`: yeni olusturulan ve henuz sevk edilmemis siparis.
- `PartiallyShipped`: kalemlerden en az biri kismen veya tamamen sevk edildi, ancak siparis tamamen kapanmadi.
- `Shipped`: tum siparis kalemleri sevk edildi.
- `Cancelled`: iptal edilmis siparis.

Siparis kalemi cevabinda `quantity`, `shippedQuantity` ve `returnedQuantity` alanlari doner. Siparise bagli sevkiyat olusturulurken her kalem icin `shipment.quantity <= quantity - shippedQuantity` kurali uygulanir. Siparise bagli iade olusturulurken `return.quantity <= shippedQuantity - returnedQuantity` kurali uygulanir. Bagimsiz sevkiyat ve bagimsiz iade akislari siparis kalemi sayaclarini degistirmez.

### Purchase Requests

`GET /api/v1/purchase-requests`

`POST /api/v1/purchase-requests`

```json
{
  "supplierId": "00000000-0000-0000-0000-000000000000",
  "supplierName": "Telco Tedarik A.S.",
  "warehouseId": "00000000-0000-0000-0000-000000000000",
  "notes": "Acil tedarik",
  "items": [
    {
      "productId": "00000000-0000-0000-0000-000000000000",
      "quantity": 10
    }
  ]
}
```

`POST /api/v1/purchase-requests/{id}/approve`

`POST /api/v1/purchase-requests/{id}/receive`

Body gonderilmezse alım talebinde kalan tum miktar teslim alınır. Kısmi teslim için body:

```json
{
  "items": [
    {
      "productId": "00000000-0000-0000-0000-000000000000",
      "quantity": 4
    }
  ]
}
```

Alım talebi durum modeli:

- `PendingApproval`: onay bekleyen talep.
- `Approved`: onaylandı, henuz teslim alınmadı.
- `PartiallyReceived`: kalemlerden en az biri kısmen teslim alındı.
- `Received`: tum kalemler teslim alındı.
- `Cancelled`: iptal edildi.

Alım talebi kalemi cevabında `quantity` talep edilen miktarı, `receivedQuantity` teslim alınan miktarı ifade eder. Kısmi teslimde `receive.quantity <= quantity - receivedQuantity` kuralı uygulanır. Teslim alma yalnızca `Approved` veya `PartiallyReceived` durumundaki talepler için çalışır.

Alim talebi olusturma ve onaylama stok degistirmez. `receive` cagrisi ilgili depo bakiyesini ve toplam urun stok miktarini artirir.

### Shipments

`GET /api/v1/shipments`

`POST /api/v1/shipments`

```json
{
  "salesOrderId": "00000000-0000-0000-0000-000000000000",
  "customerId": "00000000-0000-0000-0000-000000000000",
  "recipientName": "Techno Market A.S.",
  "warehouseId": "00000000-0000-0000-0000-000000000000",
  "trackingNumber": "SVK-2026-001",
  "notes": "Kargo teslimi",
  "items": [
    {
      "productId": "00000000-0000-0000-0000-000000000000",
      "quantity": 2
    }
  ]
}
```

Sevkiyat olusturmak stoktan dusum yapar. `salesOrderId` verilirse ilgili siparis sevk edildi durumuna alinir.

### Returns

`GET /api/v1/returns`

`POST /api/v1/returns`

```json
{
  "salesOrderId": null,
  "customerId": "00000000-0000-0000-0000-000000000000",
  "customerName": "Techno Market A.S.",
  "warehouseId": "00000000-0000-0000-0000-000000000000",
  "reason": "Hasarli paket",
  "items": [
    {
      "productId": "00000000-0000-0000-0000-000000000000",
      "quantity": 1
    }
  ]
}
```

Iade olusturmak ilgili depo bakiyesini ve toplam urun stok miktarini artirir.

## Reports

`GET /api/v1/reports/current-stock`

`GET /api/v1/reports/critical-stock`

`GET /api/v1/reports/movements`

`GET /api/v1/reports/count-differences/{countId}`

## Exports

Excel `.xlsx` çıktıları:

`GET /api/v1/exports/current-stock.xlsx`

`GET /api/v1/exports/critical-stock.xlsx`

`GET /api/v1/exports/movements.xlsx`

`GET /api/v1/exports/count-differences/{countId}.xlsx`

Async export job endpoints:

- `POST /api/v1/exports/jobs`
- `GET /api/v1/exports/jobs/{jobId}`
- `GET /api/v1/exports/jobs/{jobId}/download`

Job response fields include `status`, `completedAt`, `expiresAt`, `nextAttemptAt`, `failedReasonCode` and `errorMessage`. Failed attempts are retried with exponential backoff until `MaxRetryCount`; expired files and retained terminal rows are cleaned by the background worker.

## Error Format

Hatalar `application/problem+json` formatındadır.

```json
{
  "type": "https://stokio.local/problems/product_not_found",
  "title": "product_not_found",
  "status": 404,
  "detail": "Product was not found."
}
```
