import type {
  AuthResponse,
  AuditLog,
  Category,
  CountDifference,
  CriticalStock,
  DashboardSummary,
  ExportJob,
  ExportJobType,
  Customer,
  InventoryCount,
  InventoryCountItem,
  ManagedUser,
  MetricsSnapshot,
  PagedResult,
  Product,
  PurchaseRequest,
  ReturnRequest,
  SalesOrder,
  Shipment,
  StockConsistency,
  StockMovement,
  StockMovementType,
  StockTransfer,
  Supplier,
  Warehouse,
  WarehouseStock
} from "../../types";

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? "";
const API_PREFIX = "/api/v1";
const REFRESH_REQUEST_HEADER = "X-STOKIO-Refresh";
const REFRESH_REQUEST_HEADER_VALUE = "1";

export type ApiClient = ReturnType<typeof createApiClient>;

type QueryValue = string | number | boolean | null | undefined;
type PageQuery = {
  page?: number;
  pageSize?: number;
};
type ProductListQuery = PageQuery & {
  search?: string;
  categoryId?: string | null;
  isActive?: boolean | null;
};
type PartyListQuery = PageQuery & {
  search?: string;
  isActive?: boolean | null;
};
type SearchableListQuery = PageQuery & {
  search?: string;
  isActive?: boolean | null;
};
type StatusListQuery<TStatus extends string> = PageQuery & {
  search?: string;
  status?: TStatus | null;
};
type WarehouseStockListQuery = PageQuery & {
  warehouseId?: string | null;
  productId?: string | null;
};
type StockMovementListQuery = PageQuery & {
  productId?: string | null;
  warehouseId?: string | null;
  type?: StockMovementType | null;
  from?: string | null;
  to?: string | null;
};
type AuditLogListQuery = PageQuery & {
  search?: string;
  action?: string | null;
  entityName?: string | null;
  from?: string | null;
  to?: string | null;
};

function toQueryString(params: Record<string, QueryValue> = {}) {
  const query = new URLSearchParams();
  for (const [key, value] of Object.entries(params)) {
    if (value === null || value === undefined || value === "") {
      continue;
    }

    query.set(key, String(value));
  }

  const value = query.toString();
  return value ? `?${value}` : "";
}

export function createApiClient(token: string | null) {
  async function request<T>(path: string, options: RequestInit = {}): Promise<T> {
    const headers = new Headers(options.headers);
    headers.set("Content-Type", "application/json");

    if (token) {
      headers.set("Authorization", `Bearer ${token}`);
    }

    const response = await fetch(`${API_BASE_URL}${path}`, {
      ...options,
      credentials: options.credentials ?? "same-origin",
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

  async function downloadFile(path: string, fileName: string) {
    const headers = new Headers();
    if (token) {
      headers.set("Authorization", `Bearer ${token}`);
    }

    const response = await fetch(`${API_BASE_URL}${path}`, { headers });
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
  }

  return {
    downloadExport: (path: string, fileName: string) => downloadFile(`${API_PREFIX}${path}`, fileName),

    createExportJob: (body: {
      type: ExportJobType;
      countId?: string | null;
      from?: string | null;
      to?: string | null;
    }) => request<ExportJob>(`${API_PREFIX}/exports/jobs`, { method: "POST", body: JSON.stringify(body) }),

    getExportJob: (jobId: string) => request<ExportJob>(`${API_PREFIX}/exports/jobs/${jobId}`),

    downloadExportJob: (jobId: string, fileName: string) =>
      downloadFile(`${API_PREFIX}/exports/jobs/${jobId}/download`, fileName),

    registerTenant: (body: {
      businessName: string;
      tenantSlug: string;
      ownerName: string;
      email: string;
      password: string;
      taxNumber?: string | null;
      phone?: string | null;
    }) =>
      request<AuthResponse>(`${API_PREFIX}/auth/register-tenant`, {
        method: "POST",
        credentials: "include",
        body: JSON.stringify(body)
      }),

    login: (body: { tenantSlug: string; email: string; password: string }) =>
      request<AuthResponse>(`${API_PREFIX}/auth/login`, {
        method: "POST",
        credentials: "include",
        body: JSON.stringify(body)
      }),

    refreshSession: () =>
      request<AuthResponse>(`${API_PREFIX}/auth/refresh`, {
        method: "POST",
        credentials: "include",
        headers: { [REFRESH_REQUEST_HEADER]: REFRESH_REQUEST_HEADER_VALUE }
      }),

    logout: () =>
      request<void>(`${API_PREFIX}/auth/logout`, {
        method: "POST",
        credentials: "include",
        headers: { [REFRESH_REQUEST_HEADER]: REFRESH_REQUEST_HEADER_VALUE }
      }),

    getDashboardSummary: () => request<DashboardSummary>(`${API_PREFIX}/dashboard/summary`),

    listAuditLogs: (params: AuditLogListQuery = {}) =>
      request<PagedResult<AuditLog>>(`${API_PREFIX}/observability/audit-logs${toQueryString(params)}`),

    getMetrics: () => request<MetricsSnapshot>(`${API_PREFIX}/observability/metrics`),

    listProducts: (params: ProductListQuery = {}) =>
      request<PagedResult<Product>>(`${API_PREFIX}/products${toQueryString(params)}`),

    createProduct: (body: {
      sku: string;
      name: string;
      description: string | null;
      categoryName: string | null;
      criticalStockLevel: number;
      initialStock: number;
      barcodes: string[];
    }) => request<Product>(`${API_PREFIX}/products`, { method: "POST", body: JSON.stringify(body) }),

    listCategories: (params: SearchableListQuery = {}) =>
      request<PagedResult<Category>>(`${API_PREFIX}/categories${toQueryString(params)}`),

    createCategory: (body: { name: string }) =>
      request<Category>(`${API_PREFIX}/categories`, { method: "POST", body: JSON.stringify(body) }),

    listCustomers: (params: PartyListQuery = {}) =>
      request<PagedResult<Customer>>(`${API_PREFIX}/customers${toQueryString(params)}`),

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

    listSuppliers: (params: PartyListQuery = {}) =>
      request<PagedResult<Supplier>>(`${API_PREFIX}/suppliers${toQueryString(params)}`),

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

    listUsers: (params: SearchableListQuery = {}) =>
      request<PagedResult<ManagedUser>>(`${API_PREFIX}/users${toQueryString(params)}`),

    createUser: (body: { fullName: string; email: string; password: string; role: "Manager" | "Staff" }) =>
      request<ManagedUser>(`${API_PREFIX}/users`, { method: "POST", body: JSON.stringify(body) }),

    listWarehouses: (params: SearchableListQuery = {}) =>
      request<PagedResult<Warehouse>>(`${API_PREFIX}/warehouses${toQueryString(params)}`),

    createWarehouse: (body: { code: string; name: string; address: string | null; isDefault: boolean }) =>
      request<Warehouse>(`${API_PREFIX}/warehouses`, { method: "POST", body: JSON.stringify(body) }),

    listWarehouseStock: (params: WarehouseStockListQuery = {}) =>
      request<PagedResult<WarehouseStock>>(`${API_PREFIX}/warehouses/stocks${toQueryString(params)}`),

    listOrders: (params: StatusListQuery<SalesOrder["status"]> = {}) =>
      request<PagedResult<SalesOrder>>(`${API_PREFIX}/orders${toQueryString(params)}`),

    createOrder: (body: {
      customerId: string | null;
      customerName: string;
      warehouseId: string | null;
      notes: string | null;
      items: { productId: string; quantity: number }[];
    }) => request<SalesOrder>(`${API_PREFIX}/orders`, { method: "POST", body: JSON.stringify(body) }),

    listPurchaseRequests: (params: StatusListQuery<PurchaseRequest["status"]> = {}) =>
      request<PagedResult<PurchaseRequest>>(`${API_PREFIX}/purchase-requests${toQueryString(params)}`),

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

    listShipments: (params: StatusListQuery<Shipment["status"]> = {}) =>
      request<PagedResult<Shipment>>(`${API_PREFIX}/shipments${toQueryString(params)}`),

    createShipment: (body: {
      salesOrderId: string | null;
      customerId: string | null;
      recipientName: string;
      warehouseId: string | null;
      trackingNumber: string | null;
      notes: string | null;
      items: { productId: string; quantity: number }[];
    }) => request<Shipment>(`${API_PREFIX}/shipments`, { method: "POST", body: JSON.stringify(body) }),

    listReturns: (params: StatusListQuery<ReturnRequest["status"]> = {}) =>
      request<PagedResult<ReturnRequest>>(`${API_PREFIX}/returns${toQueryString(params)}`),

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

    listStockMovements: (params: StockMovementListQuery = {}) =>
      request<PagedResult<StockMovement>>(`${API_PREFIX}/stock/movements${toQueryString(params)}`),

    listCriticalStock: () => request<CriticalStock[]>(`${API_PREFIX}/stock/critical`),

    checkStockConsistency: () => request<StockConsistency[]>(`${API_PREFIX}/stock/consistency`),

    createCount: (name: string, warehouseId: string | null) =>
      request<InventoryCount>(`${API_PREFIX}/counts`, { method: "POST", body: JSON.stringify({ name, warehouseId }) }),

    getCount: (countId: string) => request<InventoryCount>(`${API_PREFIX}/counts/${countId}`),

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
