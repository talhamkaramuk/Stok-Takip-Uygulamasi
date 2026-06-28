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
import type {
  Category,
  CriticalStock,
  Customer,
  InventoryCount,
  ManagedUser,
  Product,
  PurchaseRequest,
  ReturnRequest,
  SalesOrder,
  Shipment,
  StockMovement,
  Supplier,
  Warehouse,
  WarehouseStock
} from "../types";

export function buildPageMetrics(
  tab: TabKey,
  data: {
    products: Product[];
    categories: Category[];
    customers: Customer[];
    suppliers: Supplier[];
    warehouses: Warehouse[];
    warehouseStock: WarehouseStock[];
    critical: CriticalStock[];
    movements: StockMovement[];
    orders: SalesOrder[];
    purchaseRequests: PurchaseRequest[];
    shipments: Shipment[];
    returns: ReturnRequest[];
    activeCount: InventoryCount | null;
    users: ManagedUser[];
  }
): MetricItem[] {
  const activeProducts = data.products.filter((product) => product.isActive).length;
  const totalStock = data.products.reduce((sum, product) => sum + product.currentStock, 0);
  const stockIn = data.movements.filter((movement) => movement.type === "In" || movement.type === "TransferIn").length;
  const stockOut = data.movements.filter((movement) => movement.type === "Out" || movement.type === "TransferOut").length;
  const barcodedProducts = data.products.filter((product) => product.barcodes.length > 0).length;
  const activeWarehouses = data.warehouses.filter((warehouse) => warehouse.isActive).length;

  switch (tab) {
    case "dashboard":
      return [
        { label: "Aktif ürün", value: activeProducts.toString(), icon: <PackagePlus size={19} /> },
        { label: "Toplam stok", value: totalStock.toString(), icon: <Boxes size={19} /> },
        { label: "Sipariş", value: data.orders.length.toString(), icon: <ClipboardCheck size={19} /> },
        { label: "Sevkiyat", value: data.shipments.length.toString(), icon: <Truck size={19} /> },
        { label: "Kritik stok", value: data.critical.length.toString(), icon: <AlertTriangle size={19} />, tone: data.critical.length > 0 ? "warn" : "ok" }
      ];
    case "products":
      return [
        { label: "Aktif ürün", value: activeProducts.toString(), icon: <PackagePlus size={19} /> },
        { label: "Barkodlu ürün", value: barcodedProducts.toString(), icon: <ScanLine size={19} /> },
        { label: "Kategori", value: data.categories.length.toString(), icon: <Tags size={19} /> },
        { label: "Toplam stok", value: totalStock.toString(), icon: <Boxes size={19} /> },
        { label: "Kritik stok", value: data.critical.length.toString(), icon: <AlertTriangle size={19} />, tone: data.critical.length > 0 ? "warn" : "ok" }
      ];
    case "customers":
      return [
        { label: "Toplam müşteri", value: data.customers.length.toString(), icon: <Users size={19} /> },
        { label: "Aktif müşteri", value: data.customers.filter((customer) => customer.isActive).length.toString(), icon: <Check size={19} />, tone: "ok" },
        { label: "Pasif müşteri", value: data.customers.filter((customer) => !customer.isActive).length.toString(), icon: <AlertTriangle size={19} /> }
      ];
    case "suppliers":
      return [
        { label: "Toplam tedarikçi", value: data.suppliers.length.toString(), icon: <Handshake size={19} /> },
        { label: "Aktif tedarikçi", value: data.suppliers.filter((supplier) => supplier.isActive).length.toString(), icon: <Check size={19} />, tone: "ok" },
        { label: "Pasif tedarikçi", value: data.suppliers.filter((supplier) => !supplier.isActive).length.toString(), icon: <AlertTriangle size={19} /> }
      ];
    case "orders":
      return [
        { label: "Toplam sipariş", value: data.orders.length.toString(), icon: <ClipboardCheck size={19} /> },
        { label: "Bekleyen", value: data.orders.filter((order) => order.status === "Pending").length.toString(), icon: <ClipboardList size={19} /> },
        { label: "Kısmi sevk", value: data.orders.filter((order) => order.status === "PartiallyShipped").length.toString(), icon: <Truck size={19} /> },
        { label: "Sevk edildi", value: data.orders.filter((order) => order.status === "Shipped").length.toString(), icon: <Truck size={19} /> },
        { label: "İptal", value: data.orders.filter((order) => order.status === "Cancelled").length.toString(), icon: <AlertTriangle size={19} />, tone: "warn" }
      ];
    case "purchase":
      return [
        { label: "Toplam talep", value: data.purchaseRequests.length.toString(), icon: <Download size={19} /> },
        { label: "Onay bekliyor", value: data.purchaseRequests.filter((request) => request.status === "PendingApproval").length.toString(), icon: <ClipboardList size={19} /> },
        { label: "Onaylandı", value: data.purchaseRequests.filter((request) => request.status === "Approved").length.toString(), icon: <Check size={19} />, tone: "ok" },
        { label: "Kısmi teslim", value: data.purchaseRequests.filter((request) => request.status === "PartiallyReceived").length.toString(), icon: <Boxes size={19} /> },
        { label: "Teslim alındı", value: data.purchaseRequests.filter((request) => request.status === "Received").length.toString(), icon: <Boxes size={19} /> },
      ];
    case "shipments":
      return [
        { label: "Toplam sevkiyat", value: data.shipments.length.toString(), icon: <Truck size={19} /> },
        { label: "Tamamlandı", value: data.shipments.filter((shipment) => shipment.status === "Completed").length.toString(), icon: <Check size={19} />, tone: "ok" },
        { label: "İptal", value: data.shipments.filter((shipment) => shipment.status === "Cancelled").length.toString(), icon: <AlertTriangle size={19} />, tone: "warn" },
        { label: "Sevk edilen adet", value: data.shipments.reduce((sum, shipment) => sum + shipment.totalQuantity, 0).toString(), icon: <Boxes size={19} /> }
      ];
    case "returns":
      return [
        { label: "Toplam iade", value: data.returns.length.toString(), icon: <RotateCcw size={19} /> },
        { label: "Teslim alındı", value: data.returns.filter((item) => item.status === "Received").length.toString(), icon: <Check size={19} />, tone: "ok" },
        { label: "Reddedildi", value: data.returns.filter((item) => item.status === "Rejected").length.toString(), icon: <AlertTriangle size={19} />, tone: "warn" },
        { label: "İade adedi", value: data.returns.reduce((sum, item) => sum + item.totalQuantity, 0).toString(), icon: <Boxes size={19} /> }
      ];
    case "warehouses":
      return [
        { label: "Aktif depo", value: activeWarehouses.toString(), icon: <Boxes size={19} /> },
        { label: "Varsayılan depo", value: data.warehouses.filter((warehouse) => warehouse.isDefault).length.toString(), icon: <Check size={19} />, tone: "ok" },
        { label: "Depo stoku", value: data.warehouseStock.reduce((sum, item) => sum + item.quantity, 0).toString(), icon: <PackagePlus size={19} /> },
        { label: "Kritik depo satırı", value: data.warehouseStock.filter((item) => item.isCritical).length.toString(), icon: <AlertTriangle size={19} />, tone: data.warehouseStock.some((item) => item.isCritical) ? "warn" : "ok" }
      ];
    case "stock":
      return [
        { label: "Hareket", value: data.movements.length.toString(), icon: <ClipboardList size={19} /> },
        { label: "Giriş", value: stockIn.toString(), icon: <Download size={19} />, tone: "ok" },
        { label: "Çıkış", value: stockOut.toString(), icon: <Truck size={19} /> },
        { label: "Sayım düzeltme", value: data.movements.filter((movement) => movement.type === "CountCorrection").length.toString(), icon: <ScanLine size={19} /> }
      ];
    case "count":
      return [
        { label: "Sayım durumu", value: data.activeCount?.status === "Open" ? "Açık" : "Kapalı", icon: <ScanLine size={19} />, tone: data.activeCount?.status === "Open" ? "ok" : undefined },
        { label: "Sayılan ürün", value: (data.activeCount?.itemCount ?? 0).toString(), icon: <Boxes size={19} /> },
        { label: "Fark", value: (data.activeCount?.differenceCount ?? 0).toString(), icon: <AlertTriangle size={19} />, tone: (data.activeCount?.differenceCount ?? 0) > 0 ? "warn" : "ok" },
        { label: "Barkodlu ürün", value: barcodedProducts.toString(), icon: <ScanLine size={19} /> }
      ];
    case "users":
      return [
        { label: "Toplam kullanıcı", value: data.users.length.toString(), icon: <Users size={19} /> },
        { label: "Aktif kullanıcı", value: data.users.filter((user) => user.isActive).length.toString(), icon: <Check size={19} />, tone: "ok" },
        { label: "Yönetici", value: data.users.filter((user) => user.role === "Owner" || user.role === "Manager").length.toString(), icon: <ShieldCheck size={19} /> }
      ];
    default:
      return [];
  }
}
