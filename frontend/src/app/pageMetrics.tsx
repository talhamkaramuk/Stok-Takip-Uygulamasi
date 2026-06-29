import {
  AlertTriangle,
  Boxes,
  Check,
  ClipboardCheck,
  ClipboardList,
  Download,
  Handshake,
  PackagePlus,
  RotateCcw,
  ScanLine,
  ShieldCheck,
  Tags,
  Truck,
  Users
} from "lucide-react";
import type { MetricItem, TabKey } from "../shared/types/ui";
import type { CriticalStock, DashboardSummary, InventoryCount, Product } from "../types";

export function buildPageMetrics(
  tab: TabKey,
  data: {
    products: Product[];
    critical: CriticalStock[];
    activeCount: InventoryCount | null;
    dashboardSummary: DashboardSummary | null;
  }
): MetricItem[] {
  const summary = data.dashboardSummary;
  const activeProducts = summary?.activeProductCount ?? data.products.filter((product) => product.isActive).length;
  const totalStock = summary?.totalStock ?? data.products.reduce((sum, product) => sum + product.currentStock, 0);
  const criticalStock = summary?.criticalStockCount ?? data.critical.length;
  const barcodedProducts = data.products.filter((product) => product.barcodes.length > 0).length;

  switch (tab) {
    case "dashboard":
      return [
        { label: "Aktif ürün", value: activeProducts.toString(), icon: <PackagePlus size={19} /> },
        { label: "Toplam stok", value: totalStock.toString(), icon: <Boxes size={19} /> },
        { label: "Sipariş", value: (summary?.orderCount ?? 0).toString(), icon: <ClipboardCheck size={19} /> },
        { label: "Sevkiyat", value: (summary?.shipmentCount ?? 0).toString(), icon: <Truck size={19} /> },
        { label: "Kritik stok", value: criticalStock.toString(), icon: <AlertTriangle size={19} />, tone: criticalStock > 0 ? "warn" : "ok" }
      ];
    case "products":
      return [
        { label: "Aktif ürün", value: activeProducts.toString(), icon: <PackagePlus size={19} /> },
        { label: "Barkodlu ürün", value: barcodedProducts.toString(), icon: <ScanLine size={19} /> },
        { label: "Kategori", value: (summary?.categoryCount ?? 0).toString(), icon: <Tags size={19} /> },
        { label: "Toplam stok", value: totalStock.toString(), icon: <Boxes size={19} /> },
        { label: "Kritik stok", value: criticalStock.toString(), icon: <AlertTriangle size={19} />, tone: criticalStock > 0 ? "warn" : "ok" }
      ];
    case "customers":
      return [
        { label: "Toplam müşteri", value: (summary?.customerCount ?? 0).toString(), icon: <Users size={19} /> },
        { label: "Aktif müşteri", value: (summary?.activeCustomerCount ?? 0).toString(), icon: <Check size={19} />, tone: "ok" },
        { label: "Pasif müşteri", value: Math.max(0, (summary?.customerCount ?? 0) - (summary?.activeCustomerCount ?? 0)).toString(), icon: <AlertTriangle size={19} /> }
      ];
    case "suppliers":
      return [
        { label: "Toplam tedarikçi", value: (summary?.supplierCount ?? 0).toString(), icon: <Handshake size={19} /> },
        { label: "Aktif tedarikçi", value: (summary?.activeSupplierCount ?? 0).toString(), icon: <Check size={19} />, tone: "ok" },
        { label: "Pasif tedarikçi", value: Math.max(0, (summary?.supplierCount ?? 0) - (summary?.activeSupplierCount ?? 0)).toString(), icon: <AlertTriangle size={19} /> }
      ];
    case "orders":
      return [
        { label: "Toplam sipariş", value: (summary?.orderCount ?? 0).toString(), icon: <ClipboardCheck size={19} /> },
        { label: "Bekleyen", value: (summary?.pendingOrderCount ?? 0).toString(), icon: <ClipboardList size={19} /> },
        { label: "Kısmi sevk", value: (summary?.partiallyShippedOrderCount ?? 0).toString(), icon: <Truck size={19} /> },
        { label: "Sevk edildi", value: (summary?.shippedOrderCount ?? 0).toString(), icon: <Truck size={19} /> },
        { label: "İptal", value: (summary?.cancelledOrderCount ?? 0).toString(), icon: <AlertTriangle size={19} />, tone: "warn" }
      ];
    case "purchase":
      return [
        { label: "Toplam talep", value: (summary?.purchaseRequestCount ?? 0).toString(), icon: <Download size={19} /> },
        { label: "Onay bekliyor", value: (summary?.pendingPurchaseRequestCount ?? 0).toString(), icon: <ClipboardList size={19} /> },
        { label: "Onaylandı", value: (summary?.approvedPurchaseRequestCount ?? 0).toString(), icon: <Check size={19} />, tone: "ok" },
        { label: "Kısmi teslim", value: (summary?.partiallyReceivedPurchaseRequestCount ?? 0).toString(), icon: <Boxes size={19} /> },
        { label: "Teslim alındı", value: (summary?.receivedPurchaseRequestCount ?? 0).toString(), icon: <Boxes size={19} /> }
      ];
    case "shipments":
      return [
        { label: "Toplam sevkiyat", value: (summary?.shipmentCount ?? 0).toString(), icon: <Truck size={19} /> },
        { label: "Tamamlandı", value: (summary?.completedShipmentCount ?? 0).toString(), icon: <Check size={19} />, tone: "ok" },
        { label: "İptal", value: (summary?.cancelledShipmentCount ?? 0).toString(), icon: <AlertTriangle size={19} />, tone: "warn" }
      ];
    case "returns":
      return [
        { label: "Toplam iade", value: (summary?.returnCount ?? 0).toString(), icon: <RotateCcw size={19} /> },
        { label: "Teslim alındı", value: (summary?.receivedReturnCount ?? 0).toString(), icon: <Check size={19} />, tone: "ok" },
        { label: "Reddedildi", value: (summary?.rejectedReturnCount ?? 0).toString(), icon: <AlertTriangle size={19} />, tone: "warn" }
      ];
    case "warehouses":
      return [
        { label: "Aktif depo", value: (summary?.activeWarehouseCount ?? 0).toString(), icon: <Boxes size={19} /> },
        { label: "Toplam depo", value: (summary?.warehouseCount ?? 0).toString(), icon: <Check size={19} />, tone: "ok" },
        { label: "Toplam stok", value: totalStock.toString(), icon: <PackagePlus size={19} /> },
        { label: "Kritik stok", value: criticalStock.toString(), icon: <AlertTriangle size={19} />, tone: criticalStock > 0 ? "warn" : "ok" }
      ];
    case "stock":
      return [
        { label: "Hareket", value: (summary?.stockMovementCount ?? 0).toString(), icon: <ClipboardList size={19} /> },
        { label: "Giriş", value: (summary?.stockInMovementCount ?? 0).toString(), icon: <Download size={19} />, tone: "ok" },
        { label: "Çıkış", value: (summary?.stockOutMovementCount ?? 0).toString(), icon: <Truck size={19} /> },
        { label: "Sayım düzeltme", value: (summary?.countCorrectionMovementCount ?? 0).toString(), icon: <ScanLine size={19} /> }
      ];
    case "count":
      return [
        { label: "Sayım durumu", value: data.activeCount?.status === "Open" ? "Açık" : "Kapalı", icon: <ScanLine size={19} />, tone: data.activeCount?.status === "Open" ? "ok" : undefined },
        { label: "Sayım ürünü", value: (data.activeCount?.itemCount ?? 0).toString(), icon: <Boxes size={19} /> },
        { label: "Fark", value: (data.activeCount?.differenceCount ?? 0).toString(), icon: <AlertTriangle size={19} />, tone: (data.activeCount?.differenceCount ?? 0) > 0 ? "warn" : "ok" },
        { label: "Barkodlu ürün", value: barcodedProducts.toString(), icon: <ScanLine size={19} /> }
      ];
    case "users":
      return [
        { label: "Toplam kullanıcı", value: (summary?.userCount ?? 0).toString(), icon: <Users size={19} /> },
        { label: "Aktif kullanıcı", value: (summary?.activeUserCount ?? 0).toString(), icon: <Check size={19} />, tone: "ok" },
        { label: "Yönetici", value: "-", icon: <ShieldCheck size={19} /> }
      ];
    default:
      return [];
  }
}
