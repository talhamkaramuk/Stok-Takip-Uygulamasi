import {
  RotateCcw,
  Search
} from "lucide-react";
import type { FormEvent } from "react";
import { useState } from "react";
import type { ApiClient } from "../../shared/api/client";
import { getErrorMessage } from "../../shared/errors/getErrorMessage";
import { useServerPage } from "../../shared/pagination/useServerPage";
import type { Notice } from "../../shared/types/ui";
import { OperationTable } from "../../shared/ui/OperationTable";
import { getDefaultWarehouseId } from "../../shared/utils/inventory";
import type {
  Customer,
  Product,
  ReturnRequest,
  ReturnRequestStatus,
  SalesOrder,
  Warehouse
} from "../../types";

export function ReturnsView({
  api,
  products,
  warehouses,
  customers,
  orders,
  onChanged,
  setNotice
}: {
  api: ApiClient;
  products: Product[];
  warehouses: Warehouse[];
  customers: Customer[];
  orders: SalesOrder[];
  onChanged: () => void;
  setNotice: (notice: Notice | null) => void;
}) {
  const [form, setForm] = useState({ salesOrderId: "", customerId: "", customerName: "", productId: "", warehouseId: "", quantity: 1, reason: "" });
  const [query, setQuery] = useState("");
  const [status, setStatus] = useState<ReturnRequestStatus | "">("");
  const activeWarehouses = warehouses.filter((warehouse) => warehouse.isActive);
  const activeCustomers = customers.filter((customer) => customer.isActive);
  const returnPage = useServerPage<ReturnRequest, { search?: string; status?: ReturnRequestStatus }>({
    filters: {
      search: query.trim() || undefined,
      status: status || undefined
    },
    load: api.listReturns,
    onError: (error) => setNotice({ type: "error", message: getErrorMessage(error) })
  });
  const returnableOrders = orders.filter((order) =>
    order.status !== "Draft" &&
    order.status !== "Cancelled" &&
    order.items.some((item) => (item.shippedQuantity ?? 0) - (item.returnedQuantity ?? 0) > 0));
  const selectedWarehouseId = form.warehouseId || getDefaultWarehouseId(activeWarehouses);

  function selectOrder(orderId: string) {
    const order = orders.find((item) => item.id === orderId);
    const orderItem = order?.items.find((item) => (item.shippedQuantity ?? 0) - (item.returnedQuantity ?? 0) > 0);
    setForm({
      ...form,
      salesOrderId: orderId,
      customerId: order?.customerId ?? form.customerId,
      customerName: order?.customerName ?? form.customerName,
      warehouseId: order?.warehouseId ?? form.warehouseId,
      productId: orderItem?.productId ?? form.productId,
      quantity: orderItem ? (orderItem.shippedQuantity ?? 0) - (orderItem.returnedQuantity ?? 0) : form.quantity
    });
  }

  function selectCustomer(customerId: string) {
    const customer = activeCustomers.find((item) => item.id === customerId);
    setForm({ ...form, customerId, customerName: customer?.name ?? form.customerName });
  }

  async function submit(event: FormEvent) {
    event.preventDefault();
    setNotice(null);
    try {
      await api.createReturn({
        salesOrderId: form.salesOrderId || null,
        customerId: form.customerId || null,
        customerName: form.customerName,
        warehouseId: selectedWarehouseId || null,
        reason: form.reason,
        items: [{ productId: form.productId, quantity: form.quantity }]
      });
      setForm({ salesOrderId: "", customerId: "", customerName: "", productId: "", warehouseId: "", quantity: 1, reason: "" });
      setNotice({ type: "success", message: "İade kaydedildi ve stok girişi işlendi." });
      returnPage.reload();
      onChanged();
    } catch (error) {
      setNotice({ type: "error", message: getErrorMessage(error) });
    }
  }

  return (
    <div className="content-grid two-columns">
      <section className="tool-panel">
        <div className="section-title">
          <RotateCcw size={19} />
          <h2>İade kaydet</h2>
        </div>
        <form className="form-grid" onSubmit={submit}>
          <label>
            Kayıtlı müşteri
            <select value={form.customerId} onChange={(event) => selectCustomer(event.target.value)}>
              <option value="">Serbest müşteri</option>
              {activeCustomers.map((customer) => (
                <option key={customer.id} value={customer.id}>{customer.code} - {customer.name}</option>
              ))}
            </select>
          </label>
          <label>
            Bağlı sipariş
            <select value={form.salesOrderId} onChange={(event) => selectOrder(event.target.value)}>
              <option value="">Bağımsız iade</option>
              {returnableOrders.map((order) => (
                <option key={order.id} value={order.id}>{order.orderNumber} - {order.customerName}</option>
              ))}
            </select>
          </label>
          <label>
            Müşteri
            <input value={form.customerName} onChange={(event) => setForm({ ...form, customerName: event.target.value })} required />
          </label>
          <label>
            Ürün
            <select value={form.productId} onChange={(event) => setForm({ ...form, productId: event.target.value })} required>
              <option value="">Seç</option>
              {products.map((product) => (
                <option key={product.id} value={product.id}>{product.sku} - {product.name}</option>
              ))}
            </select>
          </label>
          <div className="inline-fields">
            <label>
              Giriş Deposu
              <select value={selectedWarehouseId} onChange={(event) => setForm({ ...form, warehouseId: event.target.value })}>
                <option value="">Varsayılan</option>
                {activeWarehouses.map((warehouse) => (
                  <option key={warehouse.id} value={warehouse.id}>{warehouse.code} - {warehouse.name}</option>
                ))}
              </select>
            </label>
            <label>
              Adet
              <input type="number" min={1} value={form.quantity} onChange={(event) => setForm({ ...form, quantity: event.target.valueAsNumber })} />
            </label>
          </div>
          <label>
            İade nedeni
            <textarea value={form.reason} onChange={(event) => setForm({ ...form, reason: event.target.value })} rows={3} required />
          </label>
          <button className="primary-action" type="submit">
            <RotateCcw size={17} />
            İade Kaydet
          </button>
        </form>
      </section>

      <div className="content-grid">
        <section className="tool-panel compact-filter-panel">
          <div className="table-filter-row">
            <label className="search-field">
              <Search size={16} />
              <input value={query} onChange={(event) => setQuery(event.target.value)} placeholder="İade no, sipariş, müşteri veya neden ara" />
            </label>
            <select value={status} onChange={(event) => setStatus(event.target.value as ReturnRequestStatus | "")}>
              <option value="">Tüm durumlar</option>
              <option value="Received">Teslim alındı</option>
              <option value="Rejected">Reddedildi</option>
            </select>
          </div>
        </section>
        <OperationTable
          title="İadeler"
          icon={<RotateCcw size={19} />}
          rows={returnPage.items.map((item) => ({
            id: item.id,
            number: item.returnNumber,
            party: item.customerName,
            warehouse: item.warehouseName || "-",
            status: item.status,
            quantity: item.totalQuantity,
            date: item.receivedAt
          }))}
          pagination={{
            page: returnPage.page,
            totalPages: returnPage.totalPages,
            totalCount: returnPage.totalCount,
            startIndex: returnPage.startIndex,
            endIndex: returnPage.endIndex,
            onPageChange: returnPage.setPage
          }}
        />
      </div>
    </div>
  );
}
