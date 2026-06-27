export type UserRole = "Owner" | "Manager" | "Staff";

export type PagedResult<T> = {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
};

export type AuthResponse = {
  accessToken: string;
  expiresAt: string;
  user: {
    id: string;
    tenantId: string;
    tenantSlug: string;
    fullName: string;
    email: string;
    role: UserRole;
  };
};

export type Product = {
  id: string;
  sku: string;
  name: string;
  description?: string | null;
  categoryName?: string | null;
  criticalStockLevel: number;
  currentStock: number;
  isActive: boolean;
  barcodes: string[];
};

export type Category = {
  id: string;
  name: string;
  isActive: boolean;
  productCount: number;
};

export type Customer = {
  id: string;
  code: string;
  name: string;
  contactName: string | null;
  email: string | null;
  phone: string | null;
  taxNumber: string | null;
  address: string | null;
  notes: string | null;
  isActive: boolean;
  createdAt: string;
};

export type Supplier = {
  id: string;
  code: string;
  name: string;
  contactName: string | null;
  email: string | null;
  phone: string | null;
  taxNumber: string | null;
  address: string | null;
  notes: string | null;
  isActive: boolean;
  createdAt: string;
};

export type ManagedUser = {
  id: string;
  fullName: string;
  email: string;
  role: UserRole;
  isActive: boolean;
  createdAt: string;
};

export type StockMovementType = "In" | "Out" | "Adjustment" | "CountCorrection" | "TransferIn" | "TransferOut";

export type StockMovement = {
  id: string;
  productId: string;
  productName: string;
  sku: string;
  warehouseId: string | null;
  warehouseName: string | null;
  type: StockMovementType;
  quantity: number;
  previousQuantity: number;
  newQuantity: number;
  reason?: string | null;
  createdAt: string;
};

export type Warehouse = {
  id: string;
  code: string;
  name: string;
  address: string | null;
  isDefault: boolean;
  isActive: boolean;
  productCount: number;
  totalQuantity: number;
};

export type WarehouseStock = {
  warehouseId: string;
  warehouseCode: string;
  warehouseName: string;
  productId: string;
  sku: string;
  productName: string;
  quantity: number;
  criticalStockLevel: number;
  isCritical: boolean;
};

export type StockTransfer = {
  transferGroupId: string;
  productId: string;
  sku: string;
  productName: string;
  fromWarehouseId: string;
  fromWarehouseName: string;
  toWarehouseId: string;
  toWarehouseName: string;
  quantity: number;
  createdAt: string;
};

export type CriticalStock = {
  productId: string;
  sku: string;
  productName: string;
  currentStock: number;
  criticalStockLevel: number;
};

export type StockConsistency = {
  productId: string;
  sku: string;
  productName: string;
  storedCurrentStock: number;
  ledgerCurrentStock: number;
  isConsistent: boolean;
  issues: string[];
};

export type InventoryCount = {
  id: string;
  name: string;
  warehouseId: string | null;
  warehouseName: string | null;
  status: "Draft" | "Open" | "Closed" | "Cancelled";
  startedAt: string;
  closedAt?: string | null;
  itemCount: number;
  differenceCount: number;
};

export type InventoryCountItem = {
  productId: string;
  sku: string;
  productName: string;
  expectedQuantity: number;
  countedQuantity: number;
  difference: number;
};

export type CountDifference = InventoryCountItem;

export type OperationItem = {
  productId: string;
  sku: string;
  productName: string;
  quantity: number;
};

export type SalesOrderStatus = "Preparing" | "Shipped" | "Completed" | "Cancelled";

export type SalesOrder = {
  id: string;
  orderNumber: string;
  customerId: string | null;
  customerName: string;
  warehouseId: string | null;
  warehouseName: string | null;
  status: SalesOrderStatus;
  lineCount: number;
  totalQuantity: number;
  notes: string | null;
  createdAt: string;
  items: OperationItem[];
};

export type PurchaseRequestStatus = "PendingApproval" | "Approved" | "Received" | "Cancelled";

export type PurchaseRequest = {
  id: string;
  requestNumber: string;
  supplierId: string | null;
  supplierName: string;
  warehouseId: string | null;
  warehouseName: string | null;
  status: PurchaseRequestStatus;
  lineCount: number;
  totalQuantity: number;
  notes: string | null;
  approvedAt: string | null;
  receivedAt: string | null;
  createdAt: string;
  items: OperationItem[];
};

export type ShipmentStatus = "Completed" | "Cancelled";

export type Shipment = {
  id: string;
  shipmentNumber: string;
  salesOrderId: string | null;
  salesOrderNumber: string | null;
  customerId: string | null;
  recipientName: string;
  warehouseId: string | null;
  warehouseName: string | null;
  trackingNumber: string | null;
  status: ShipmentStatus;
  lineCount: number;
  totalQuantity: number;
  shippedAt: string;
  createdAt: string;
  items: OperationItem[];
};

export type ReturnRequestStatus = "Received" | "Rejected";

export type ReturnRequest = {
  id: string;
  returnNumber: string;
  salesOrderId: string | null;
  salesOrderNumber: string | null;
  customerId: string | null;
  customerName: string;
  warehouseId: string | null;
  warehouseName: string | null;
  reason: string;
  status: ReturnRequestStatus;
  lineCount: number;
  totalQuantity: number;
  receivedAt: string;
  createdAt: string;
  items: OperationItem[];
};
