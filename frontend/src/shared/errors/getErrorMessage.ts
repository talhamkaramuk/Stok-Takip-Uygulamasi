export function getErrorMessage(error: unknown) {
  if (!(error instanceof Error)) {
    return "İşlem tamamlanamadı.";
  }

  return translateApiError(error.message);
}

function translateApiError(message: string) {
  const exactTranslations: Record<string, string> = {
    "Barcode was not assigned to an active product.": "Bu barkod aktif bir ürüne tanımlı değil. Ürünler sayfasında barkodu ürüne ekleyin.",
    "Warehouse was not found.": "Depo bulunamadı. İşleme geçerli bir depo ile devam edin.",
    "Product was not found.": "Ürün bulunamadı.",
    "One or more products were not found.": "Bir veya daha fazla ürün bulunamadı.",
    "Customer was not found.": "Müşteri bulunamadı veya pasif durumda.",
    "Supplier was not found.": "Tedarikçi bulunamadı veya pasif durumda.",
    "Sales order was not found.": "Sipariş bulunamadı.",
    "Purchase request was not found.": "Alım talebi bulunamadı.",
    "Inventory count was not found.": "Sayım bulunamadı.",
    "Only open inventory counts can be changed.": "Sadece açık sayımlar değiştirilebilir.",
    "Only pending purchase requests can be approved.": "Sadece onay bekleyen alım talepleri onaylanabilir.",
    "Closed purchase requests cannot be received.": "Kapanmış alım talepleri teslim alınamaz.",
    "Stock cannot be reduced below zero.": "Stok sıfırın altına düşürülemez.",
    "Source warehouse stock is insufficient.": "Kaynak depodaki stok yetersiz.",
    "Source and target warehouses must be different.": "Kaynak ve hedef depo farklı olmalıdır.",
    "Default warehouse cannot be deactivated.": "Varsayılan depo pasife alınamaz.",
    "Warehouse with stock cannot be deactivated.": "İçinde stok bulunan depo pasife alınamaz.",
    "A warehouse with this code already exists.": "Bu koda sahip bir depo zaten var.",
    "A product with this SKU already exists.": "Bu SKU ile kayıtlı bir ürün zaten var.",
    "One or more barcodes are already assigned to a product.": "Barkodlardan biri veya daha fazlası başka bir ürüne atanmış.",
    "A category with this name already exists.": "Bu isimde bir kategori zaten var.",
    "A customer with this code already exists.": "Bu koda sahip bir müşteri zaten var.",
    "A supplier with this code already exists.": "Bu koda sahip bir tedarikçi zaten var.",
    "A user with this email already exists.": "Bu e-posta ile kayıtlı bir kullanıcı zaten var.",
    "Invalid tenant, email, or password.": "Tenant, e-posta veya şifre hatalı.",
    "This tenant slug is already in use.": "Bu tenant kısa adı zaten kullanılıyor.",
    "Tenant context is required.": "Tenant bağlamı gerekli."
  };

  if (exactTranslations[message]) {
    return exactTranslations[message];
  }

  return message
    .replaceAll("must not be empty", "boş olamaz")
    .replaceAll("must be greater than '0'", "0'dan büyük olmalıdır")
    .replaceAll("must be greater than or equal to '0'", "0 veya daha büyük olmalıdır")
    .replaceAll("is not a valid email address", "geçerli bir e-posta adresi değil");
}