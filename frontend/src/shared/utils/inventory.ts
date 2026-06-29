import type { Product, Warehouse } from "../../types";

export function getDefaultWarehouseId(warehouses: Warehouse[]) {
  return warehouses.find((warehouse) => warehouse.isDefault)?.id ?? warehouses[0]?.id ?? "";
}

export function isActiveWarehouseId(warehouses: Warehouse[], warehouseId: string) {
  return Boolean(warehouseId) && warehouses.some((warehouse) => warehouse.id === warehouseId);
}

export function findProductByBarcode(products: Product[], barcode: string) {
  const normalizedBarcode = barcode.trim();
  return products.find((product) =>
    product.isActive && product.barcodes.some((value) => value.trim() === normalizedBarcode));
}

export function appendBarcode(currentValue: string, barcode: string) {
  const normalizedBarcode = barcode.trim();
  if (!normalizedBarcode) {
    return currentValue;
  }

  const existingBarcodes = currentValue
    .split(/\r?\n|,/)
    .map((value) => value.trim())
    .filter(Boolean);

  if (existingBarcodes.includes(normalizedBarcode)) {
    return existingBarcodes.join("\n");
  }

  return [...existingBarcodes, normalizedBarcode].join("\n");
}

export function emptyToNull(value: string) {
  const trimmed = value.trim();
  return trimmed ? trimmed : null;
}

export function statusClass(status: string) {
  return status === "Cancelled" || status === "Rejected" ? "pill warn" : "pill";
}

export function statusLabel(status: string) {
  const labels: Record<string, string> = {
    Draft: "Taslak",
    Pending: "Bekliyor",
    PartiallyShipped: "Kısmi Sevk Edildi",
    Preparing: "Hazırlanıyor",
    Shipped: "Sevk Edildi",
    Completed: "Tamamlandı",
    Cancelled: "İptal",
    PendingApproval: "Onay Bekliyor",
    Approved: "Onaylandı",
    PartiallyReceived: "Kısmi Teslim Alındı",
    Received: "Teslim Alındı",
    Rejected: "Reddedildi"
  };

  return labels[status] ?? status;
}

export function statusMovementLabel(type: string) {
  const labels: Record<string, string> = {
    In: "Giriş",
    Out: "Çıkış",
    Adjustment: "Düzeltme",
    CountCorrection: "Sayım düzeltme",
    TransferIn: "Transfer giriş",
    TransferOut: "Transfer çıkış"
  };

  return labels[type] ?? type;
}

export function formatDate(value: string) {
  return new Date(value).toLocaleString("tr-TR");
}
