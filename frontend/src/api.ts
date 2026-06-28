import type {
  AuthResponse,
  Category,
  Customer,
  CountDifference,
  CriticalStock,
  InventoryCount,
  InventoryCountItem,
  ManagedUser,
  PagedResult,
  Product,
  PurchaseRequest,
  ReturnRequest,
  SalesOrder,
  Shipment,
  StockConsistency,
  StockMovement,
  StockMovementType,
  Supplier,
  StockTransfer,
  Warehouse,
  WarehouseStock
} from "./types";

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? "";
const API_PREFIX = "/api/v1";

export type ApiClient = ReturnType<typeof createApiClient>;

export function createApiClient(token: string | null) {
  async function request<T>(path: string, options: RequestInit = {}): Promise<T> {
    const headers = new Headers(options.headers);
    headers.set("Content-Type", "application/json");

    if (token) {
      headers.set("Authorization", `Bearer ${token}`);
    }

    const response = await fetch(`${API_BASE_URL}${path}`, {
      ...options,
      headers
    });

    if (!response.ok) {
      throw new Error(await readError(response));
    }

    if (response.status === 204) {
      return undefined as T;
    }

    return (await response.json()) as T;
  }

  return {
    downloadExport: async (path: string, fileName: string) => {
      const headers = new Headers();
      if (token) {
        headers.set("Authorization", `Bearer ${token}`);
      }

      const response = await fetch(`${API_BASE_URL}${API_PREFIX}${path}`, { headers });
      if (!response.ok) {
        throw new Error(await readError(response));
      }

      const blob = await response.blob();
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement("a");
      anchor.href = url;
      anchor.download = fileName;
      document.body.append(anchor);
      anchor.click();
      anchor.remove();
      URL.revokeObjectURL(url);
    },

    registerTenant: (body: {
      businessName: string;
      tenantSlug: string;
      ownerName: string;
      email: string;
      password: string;
      taxNumber?: string | null;
      phone?: string | null;
    }) => request<AuthResponse>(`${API_PREFIX}/auth/register-tenant`, { method: "POST", body: JSON.stringify(body) }),

    login: (body: { tenantSlug: string; email: string; password: string }) =>
      request<AuthResponse>(`${API_PREFIX}/auth/login`, { method: "POST", body: JSON.stringify(body) }),

    listProducts: () => request<PagedResult<Product>>(`${API_PREFIX}/products?pageSize=100`),

    createProduct: (body: {
      sku: string;
      name: string;
      description: string | null;
      categoryName: string | null;
      criticalStockLevel: number;
      initialStock: number;
      barcodes: string[];
    }) => request<Product>(`${API_PREFIX}/products`, { method: "POST", body: JSON.stringify(body) }),

    listCategories: () => request<PagedResult<Category>>(`${API_PREFIX}/categories?pageSize=100`),

    createCategory: (body: { name: string }) =>
      request<Category>(`${API_PREFIX}/categories`, { method: "POST", body: JSON.stringify(body) }),

    listCustomers: () => request<PagedResult<Customer>>(`${API_PREFIX}/customers?pageSize=100`),

    createCustomer: (body: {
      code: string;
      name: string;
      contactName: string | null;
      email: string | null;
      phone: string | null;
      taxNumber: string | null;
      address: string | null;
      notes: string | null;
    }) => request<Customer>(`${API_PREFIX}/customers`, { method: "POST", body: JSON.stringify(body) }),

    updateCustomer: (id: string, body: {
      code: string;
      name: string;
      contactName: string | null;
      email: string | null;
      phone: string | null;
      taxNumber: string | null;
      address: string | null;
      notes: string | null;
      isActive: boolean;
    }) => request<Customer>(`${API_PREFIX}/customers/${id}`, { method: "PUT", body: JSON.stringify(body) }),

    deactivateCustomer: (id: string) =>
      request<void>(`${API_PREFIX}/customers/${id}`, { method: "DELETE" }),

    listSuppliers: () => request<PagedResult<Supplier>>(`${API_PREFIX}/suppliers?pageSize=100`),

    createSupplier: (body: {
      code: string;
      name: string;
      contactName: string | null;
      email: string | null;
      phone: string | null;
      taxNumber: string | null;
      address: string | null;
      notes: string | null;
    }) => request<Supplier>(`${API_PREFIX}/suppliers`, { method: "POST", body: JSON.stringify(body) }),

    updateSupplier: (id: string, body: {
      code: string;
      name: string;
      contactName: string | null;
      email: string | null;
      phone: string | null;
      taxNumber: string | null;
      address: string | null;
      notes: string | null;
      isActive: boolean;
    }) => request<Supplier>(`${API_PREFIX}/suppliers/${id}`, { method: "PUT", body: JSON.stringify(body) }),

    deactivateSupplier: (id: string) =>
      request<void>(`${API_PREFIX}/suppliers/${id}`, { method: "DELETE" }),

    listUsers: () => request<PagedResult<ManagedUser>>(`${API_PREFIX}/users?pageSize=100`),

    createUser: (body: { fullName: string; email: string; password: string; role: "Manager" | "Staff" }) =>
      request<ManagedUser>(`${API_PREFIX}/users`, { method: "POST", body: JSON.stringify(body) }),

    listWarehouses: () => request<PagedResult<Warehouse>>(`${API_PREFIX}/warehouses?pageSize=100`),

    createWarehouse: (body: { code: string; name: string; address: string | null; isDefault: boolean }) =>
      request<Warehouse>(`${API_PREFIX}/warehouses`, { method: "POST", body: JSON.stringify(body) }),

    listWarehouseStock: () => request<WarehouseStock[]>(`${API_PREFIX}/warehouses/stocks`),

    listOrders: () => request<PagedResult<SalesOrder>>(`${API_PREFIX}/orders?pageSize=100`),

    createOrder: (body: {
      customerId: string | null;
      customerName: string;
      warehouseId: string | null;
      notes: string | null;
      items: { productId: string; quantity: number }[];
    }) => request<SalesOrder>(`${API_PREFIX}/orders`, { method: "POST", body: JSON.stringify(body) }),

    listPurchaseRequests: () => request<PagedResult<PurchaseRequest>>(`${API_PREFIX}/purchase-requests?pageSize=100`),

    createPurchaseRequest: (body: {
      supplierId: string | null;
      supplierName: string;
      warehouseId: string | null;
      notes: string | null;
      items: { productId: string; quantity: number }[];
    }) => request<PurchaseRequest>(`${API_PREFIX}/purchase-requests`, { method: "POST", body: JSON.stringify(body) }),

    approvePurchaseRequest: (id: string) =>
      request<PurchaseRequest>(`${API_PREFIX}/purchase-requests/${id}/approve`, { method: "POST" }),

    receivePurchaseRequest: (id: string, body?: { items: { productId: string; quantity: number }[] }) =>
      request<PurchaseRequest>(`${API_PREFIX}/purchase-requests/${id}/receive`, {
        method: "POST",
        body: body ? JSON.stringify(body) : undefined
      }),

    listShipments: () => request<PagedResult<Shipment>>(`${API_PREFIX}/shipments?pageSize=100`),

    createShipment: (body: {
      salesOrderId: string | null;
      customerId: string | null;
      recipientName: string;
      warehouseId: string | null;
      trackingNumber: string | null;
      notes: string | null;
      items: { productId: string; quantity: number }[];
    }) => request<Shipment>(`${API_PREFIX}/shipments`, { method: "POST", body: JSON.stringify(body) }),

    listReturns: () => request<PagedResult<ReturnRequest>>(`${API_PREFIX}/returns?pageSize=100`),

    createReturn: (body: {
      salesOrderId: string | null;
      customerId: string | null;
      customerName: string;
      warehouseId: string | null;
      reason: string;
      items: { productId: string; quantity: number }[];
    }) => request<ReturnRequest>(`${API_PREFIX}/returns`, { method: "POST", body: JSON.stringify(body) }),

    transferStock: (body: {
      productId: string;
      fromWarehouseId: string;
      toWarehouseId: string;
      quantity: number;
      reason: string | null;
    }) => request<StockTransfer>(`${API_PREFIX}/warehouses/transfers`, { method: "POST", body: JSON.stringify(body) }),

    createStockMovement: (body: {
      productId: string;
      warehouseId: string | null;
      type: StockMovementType;
      quantity: number;
      reason: string | null;
    }) => request<StockMovement>(`${API_PREFIX}/stock/movements`, { method: "POST", body: JSON.stringify(body) }),

    listStockMovements: () => request<PagedResult<StockMovement>>(`${API_PREFIX}/stock/movements?pageSize=100`),

    listCriticalStock: () => request<CriticalStock[]>(`${API_PREFIX}/stock/critical`),

    checkStockConsistency: () => request<StockConsistency[]>(`${API_PREFIX}/stock/consistency`),

    createCount: (name: string, warehouseId: string | null) =>
      request<InventoryCount>(`${API_PREFIX}/counts`, { method: "POST", body: JSON.stringify({ name, warehouseId }) }),

    scanCountItem: (countId: string, body: { barcode: string; quantity: number }) =>
      request<InventoryCountItem>(`${API_PREFIX}/counts/${countId}/items/scan`, { method: "POST", body: JSON.stringify(body) }),

    closeCount: (countId: string, applyDifferences: boolean) =>
      request<InventoryCount>(`${API_PREFIX}/counts/${countId}/close`, {
        method: "POST",
        body: JSON.stringify({ applyDifferences })
      }),

    listCountDifferences: (countId: string) => request<CountDifference[]>(`${API_PREFIX}/counts/${countId}/differences`)
  };
}

async function readError(response: Response) {
  const fallback = `${response.status} ${response.statusText}`;
  const text = await response.text();
  if (!text) {
    return fallback;
  }

  try {
    const problem = JSON.parse(text) as { title?: string; detail?: string; errors?: Record<string, string[]> };
    if (problem.errors) {
      return Object.values(problem.errors).flat().join(" ");
    }

    return problem.detail || problem.title || fallback;
  } catch {
    return text;
  }
}
