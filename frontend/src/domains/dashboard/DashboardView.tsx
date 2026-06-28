import {
  AlertTriangle,
  ArrowLeftRight,
  BarChart3,
  Boxes,
  ClipboardCheck,
  ClipboardList,
  PackagePlus,
  Users
} from "lucide-react";
import { PaginationControls } from "../../shared/pagination/PaginationControls";
import { usePagination } from "../../shared/pagination/usePagination";
import { formatDate, statusClass, statusLabel } from "../../shared/utils/inventory";
import type {
  CriticalStock,
  Customer,
  Product,
  PurchaseRequest,
  ReturnRequest,
  SalesOrder,
  Shipment,
  StockMovement,
  Supplier,
  Warehouse,
  WarehouseStock
} from "../../types";

export function DashboardView({
  products,
  critical,
  movements,
  orders,
  purchaseRequests,
  shipments,
  returns,
  warehouses,
  warehouseStock,
  customers,
  suppliers
}: {
  products: Product[];
  critical: CriticalStock[];
  movements: StockMovement[];
  orders: SalesOrder[];
  purchaseRequests: PurchaseRequest[];
  shipments: Shipment[];
  returns: ReturnRequest[];
  warehouses: Warehouse[];
  warehouseStock: WarehouseStock[];
  customers: Customer[];
  suppliers: Supplier[];
}) {
  const operationTrend = buildOperationTrend(orders, purchaseRequests, shipments, returns);
  const stockFlow = buildStockFlow(movements);
  const operationBars = [
    { label: "Sipariş", value: orders.length, tone: "primary" },
    { label: "Alım", value: purchaseRequests.length, tone: "success" },
    { label: "Sevkiyat", value: shipments.length, tone: "info" },
    { label: "İade", value: returns.length, tone: "warning" }
  ];
  const pendingJobs = [
    { label: "Bekleyen sipariş", value: orders.filter((order) => order.status === "Pending" || order.status === "PartiallyShipped").length },
    { label: "Onay bekleyen alım", value: purchaseRequests.filter((request) => request.status === "PendingApproval").length },
    { label: "Teslim alınacak alım", value: purchaseRequests.filter((request) => request.status === "Approved" || request.status === "PartiallyReceived").length },
    { label: "Kritik stok", value: critical.length }
  ];
  const warehouseBars = buildWarehouseBars(warehouses, warehouseStock);
  const topProducts = buildTopOperationProducts(orders, purchaseRequests, shipments, returns);
  const recentOperations = buildRecentOperations(orders, purchaseRequests, shipments, returns);
  const recentOperationPagination = usePagination(recentOperations);
  const activeCustomers = customers.filter((customer) => customer.isActive).length;
  const activeSuppliers = suppliers.filter((supplier) => supplier.isActive).length;
  const stockIn = stockFlow.reduce((sum, point) => sum + point.inbound, 0);
  const stockOut = stockFlow.reduce((sum, point) => sum + point.outbound, 0);

  return (
    <div className="dashboard-grid">
      <section className="tool-panel dashboard-span-2">
        <div className="section-title spread">
          <span>
            <BarChart3 size={19} />
            <h2>Operasyon trendi</h2>
          </span>
          <small>Son 14 gün</small>
        </div>
        <OperationTrendChart points={operationTrend} />
      </section>

      <section className="tool-panel">
        <div className="section-title">
          <ClipboardList size={19} />
          <h2>Operasyon dağılımı</h2>
        </div>
        <HorizontalBars rows={operationBars} />
      </section>

      <section className="tool-panel dashboard-span-2">
        <div className="section-title spread">
          <span>
            <ArrowLeftRight size={19} />
            <h2>Stok akışı</h2>
          </span>
          <small>Giriş {stockIn} · Çıkış {stockOut}</small>
        </div>
        <StockFlowChart points={stockFlow} />
      </section>

      <section className="tool-panel">
        <div className="section-title">
          <AlertTriangle size={19} />
          <h2>Bekleyen işler</h2>
        </div>
        <div className="insight-list">
          {pendingJobs.map((item) => (
            <article className="insight-row" key={item.label}>
              <span>{item.label}</span>
              <strong>{item.value}</strong>
            </article>
          ))}
        </div>
      </section>

      <section className="tool-panel">
        <div className="section-title">
          <Boxes size={19} />
          <h2>Depo doluluğu</h2>
        </div>
        <HorizontalBars rows={warehouseBars} />
      </section>

      <section className="tool-panel">
        <div className="section-title">
          <PackagePlus size={19} />
          <h2>Operasyondaki ürünler</h2>
        </div>
        <div className="rank-list">
          {topProducts.length === 0 ? (
            <p className="empty-note">Operasyon kalemi bulunmuyor.</p>
          ) : (
            topProducts.map((item, index) => (
              <article className="rank-row" key={item.productId}>
                <span>{index + 1}</span>
                <div>
                  <strong>{item.sku}</strong>
                  <small>{item.productName}</small>
                </div>
                <b>{item.quantity}</b>
              </article>
            ))
          )}
        </div>
      </section>

      <section className="tool-panel">
        <div className="section-title">
          <Users size={19} />
          <h2>Cari kapsama</h2>
        </div>
        <div className="coverage-grid">
          <div>
            <strong>{activeCustomers}</strong>
            <span>Aktif müşteri</span>
          </div>
          <div>
            <strong>{activeSuppliers}</strong>
            <span>Aktif tedarikçi</span>
          </div>
          <div>
            <strong>{products.filter((product) => product.isActive).length}</strong>
            <span>Aktif ürün</span>
          </div>
        </div>
      </section>

      <section className="tool-panel dashboard-span-3">
        <div className="section-title">
          <ClipboardCheck size={19} />
          <h2>Son operasyonlar</h2>
        </div>
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Tip</th>
                <th>No</th>
                <th>Taraf</th>
                <th>Adet</th>
                <th>Durum</th>
                <th>Tarih</th>
              </tr>
            </thead>
            <tbody>
              {recentOperationPagination.items.map((item) => (
                <tr key={`${item.type}-${item.id}`}>
                  <td>{item.type}</td>
                  <td>{item.number}</td>
                  <td>{item.party}</td>
                  <td>{item.quantity}</td>
                  <td><span className={statusClass(item.status)}>{statusLabel(item.status)}</span></td>
                  <td>{formatDate(item.date)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <PaginationControls
          page={recentOperationPagination.page}
          totalPages={recentOperationPagination.totalPages}
          totalCount={recentOperationPagination.totalCount}
          startIndex={recentOperationPagination.startIndex}
          endIndex={recentOperationPagination.endIndex}
          onPageChange={recentOperationPagination.setPage}
        />
      </section>
    </div>
  );
}

function OperationTrendChart({ points }: { points: Array<{ label: string; total: number }> }) {
  const max = Math.max(1, ...points.map((point) => point.total));
  const width = 640;
  const height = 220;
  const padding = 26;
  const step = points.length > 1 ? (width - padding * 2) / (points.length - 1) : width - padding * 2;
  const coordinates = points.map((point, index) => {
    const x = padding + index * step;
    const y = height - padding - (point.total / max) * (height - padding * 2);
    return { ...point, x, y };
  });
  const line = coordinates.map((point) => `${point.x},${point.y}`).join(" ");
  const area = `${padding},${height - padding} ${line} ${width - padding},${height - padding}`;

  return (
    <div className="chart-shell">
      <svg viewBox={`0 0 ${width} ${height}`} role="img" aria-label="Operasyon trendi">
        <polygon className="chart-area" points={area} />
        <polyline className="chart-line" points={line} />
        {coordinates.map((point) => (
          <g key={point.label}>
            <circle className="chart-point" cx={point.x} cy={point.y} r="4" />
            <text x={point.x} y={height - 6} textAnchor="middle">{point.label}</text>
          </g>
        ))}
      </svg>
    </div>
  );
}

function StockFlowChart({ points }: { points: Array<{ label: string; inbound: number; outbound: number }> }) {
  const max = Math.max(1, ...points.flatMap((point) => [point.inbound, point.outbound]));

  return (
    <div className="flow-chart" aria-label="Stok giriş çıkış grafiği">
      {points.map((point) => (
        <div className="flow-day" key={point.label}>
          <div className="flow-bars">
            <span className="flow-in" style={{ height: `${Math.max(4, (point.inbound / max) * 100)}%` }} title={`Giriş ${point.inbound}`} />
            <span className="flow-out" style={{ height: `${Math.max(4, (point.outbound / max) * 100)}%` }} title={`Çıkış ${point.outbound}`} />
          </div>
          <small>{point.label}</small>
        </div>
      ))}
    </div>
  );
}

function HorizontalBars({ rows }: { rows: Array<{ label: string; value: number; tone?: string }> }) {
  const max = Math.max(1, ...rows.map((row) => row.value));

  return (
    <div className="bar-list">
      {rows.map((row) => (
        <article className="bar-row" key={row.label}>
          <div>
            <span>{row.label}</span>
            <strong>{row.value}</strong>
          </div>
          <b className={row.tone ? `bar-fill ${row.tone}` : "bar-fill"} style={{ width: `${Math.max(6, (row.value / max) * 100)}%` }} />
        </article>
      ))}
    </div>
  );
}

function buildOperationTrend(
  orders: SalesOrder[],
  purchaseRequests: PurchaseRequest[],
  shipments: Shipment[],
  returns: ReturnRequest[]
) {
  const days = recentDayKeys(14);
  const counts = new Map(days.map((day) => [day.key, 0]));
  const dates = [
    ...orders.map((item) => item.createdAt),
    ...purchaseRequests.map((item) => item.createdAt),
    ...shipments.map((item) => item.shippedAt),
    ...returns.map((item) => item.receivedAt)
  ];

  for (const value of dates) {
    const key = toDateKey(value);
    if (counts.has(key)) {
      counts.set(key, (counts.get(key) ?? 0) + 1);
    }
  }

  return days.map((day) => ({ label: day.label, total: counts.get(day.key) ?? 0 }));
}

function buildStockFlow(movements: StockMovement[]) {
  const days = recentDayKeys(14);
  const flow = new Map(days.map((day) => [day.key, { inbound: 0, outbound: 0 }]));

  for (const movement of movements) {
    const key = toDateKey(movement.createdAt);
    const point = flow.get(key);
    if (!point) {
      continue;
    }

    if (movement.type === "In" || movement.type === "TransferIn") {
      point.inbound += movement.quantity;
    } else if (movement.type === "Out" || movement.type === "TransferOut") {
      point.outbound += movement.quantity;
    } else if (movement.newQuantity > movement.previousQuantity) {
      point.inbound += movement.newQuantity - movement.previousQuantity;
    } else if (movement.previousQuantity > movement.newQuantity) {
      point.outbound += movement.previousQuantity - movement.newQuantity;
    }
  }

  return days.map((day) => ({ label: day.label, ...flow.get(day.key)! }));
}

function buildWarehouseBars(warehouses: Warehouse[], warehouseStock: WarehouseStock[]) {
  const quantities = new Map<string, number>();
  for (const stock of warehouseStock) {
    quantities.set(stock.warehouseId, (quantities.get(stock.warehouseId) ?? 0) + stock.quantity);
  }

  return warehouses
    .filter((warehouse) => warehouse.isActive)
    .map((warehouse) => ({
      label: warehouse.name,
      value: quantities.get(warehouse.id) ?? warehouse.totalQuantity
    }))
    .sort((left, right) => right.value - left.value)
    .slice(0, 6);
}

function buildTopOperationProducts(
  orders: SalesOrder[],
  purchaseRequests: PurchaseRequest[],
  shipments: Shipment[],
  returns: ReturnRequest[]
) {
  const totals = new Map<string, { productId: string; sku: string; productName: string; quantity: number }>();
  const allItems = [
    ...orders.flatMap((item) => item.items),
    ...purchaseRequests.flatMap((item) => item.items),
    ...shipments.flatMap((item) => item.items),
    ...returns.flatMap((item) => item.items)
  ];

  for (const item of allItems) {
    const existing = totals.get(item.productId);
    if (existing) {
      existing.quantity += item.quantity;
    } else {
      totals.set(item.productId, {
        productId: item.productId,
        sku: item.sku,
        productName: item.productName,
        quantity: item.quantity
      });
    }
  }

  return [...totals.values()].sort((left, right) => right.quantity - left.quantity).slice(0, 6);
}

function buildRecentOperations(
  orders: SalesOrder[],
  purchaseRequests: PurchaseRequest[],
  shipments: Shipment[],
  returns: ReturnRequest[]
) {
  return [
    ...orders.map((item) => ({
      id: item.id,
      type: "Sipariş",
      number: item.orderNumber,
      party: item.customerName,
      quantity: item.totalQuantity,
      status: item.status,
      date: item.createdAt
    })),
    ...purchaseRequests.map((item) => ({
      id: item.id,
      type: "Alım",
      number: item.requestNumber,
      party: item.supplierName,
      quantity: item.totalQuantity,
      status: item.status,
      date: item.createdAt
    })),
    ...shipments.map((item) => ({
      id: item.id,
      type: "Sevkiyat",
      number: item.shipmentNumber,
      party: item.recipientName,
      quantity: item.totalQuantity,
      status: item.status,
      date: item.shippedAt
    })),
    ...returns.map((item) => ({
      id: item.id,
      type: "İade",
      number: item.returnNumber,
      party: item.customerName,
      quantity: item.totalQuantity,
      status: item.status,
      date: item.receivedAt
    }))
  ]
    .sort((left, right) => new Date(right.date).getTime() - new Date(left.date).getTime())
    .slice(0, 8);
}

function recentDayKeys(dayCount: number) {
  const today = startOfLocalDay(new Date());
  return Array.from({ length: dayCount }, (_, index) => {
    const date = new Date(today);
    date.setDate(today.getDate() - (dayCount - 1 - index));
    return {
      key: toDateKey(date.toISOString()),
      label: date.toLocaleDateString("tr-TR", { day: "2-digit", month: "2-digit" })
    };
  });
}

function toDateKey(value: string) {
  return startOfLocalDay(new Date(value)).toISOString().slice(0, 10);
}

function startOfLocalDay(date: Date) {
  return new Date(date.getFullYear(), date.getMonth(), date.getDate());
}