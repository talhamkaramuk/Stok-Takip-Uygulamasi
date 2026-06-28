import {
  ClipboardCheck,
  Plus
} from "lucide-react";
import type { FormEvent } from "react";
import { useState } from "react";
import type { ApiClient } from "../../shared/api/client";
import { getErrorMessage } from "../../shared/errors/getErrorMessage";
import type { Notice } from "../../shared/types/ui";
import { OperationTable } from "../../shared/ui/OperationTable";
import { getDefaultWarehouseId } from "../../shared/utils/inventory";
import type {
  Customer,
  Product,
  SalesOrder,
  Warehouse
} from "../../types";

export function OrdersView({
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
  const [form, setForm] = useState({ customerId: "", customerName: "", productId: "", warehouseId: "", quantity: 1, notes: "" });
  const activeWarehouses = warehouses.filter((warehouse) => warehouse.isActive);
  const activeCustomers = customers.filter((customer) => customer.isActive);
  const selectedWarehouseId = form.warehouseId || getDefaultWarehouseId(activeWarehouses);

  function selectCustomer(customerId: string) {
    const customer = activeCustomers.find((item) => item.id === customerId);
    setForm({ ...form, customerId, customerName: customer?.name ?? form.customerName });
  }

  async function submit(event: FormEvent) {
    event.preventDefault();
    setNotice(null);
    try {
      await api.createOrder({
        customerId: form.customerId || null,
        customerName: form.customerName,
        warehouseId: selectedWarehouseId || null,
        notes: form.notes.trim() || null,
        items: [{ productId: form.productId, quantity: form.quantity }]
      });
      setForm({ customerId: "", customerName: "", productId: "", warehouseId: "", quantity: 1, notes: "" });
      setNotice({ type: "success", message: "Sipariş oluşturuldu." });
      onChanged();
    } catch (error) {
      setNotice({ type: "error", message: getErrorMessage(error) });
    }
  }

  return (
    <div className="content-grid two-columns">
      <section className="tool-panel">
        <div className="section-title">
          <ClipboardCheck size={19} />
          <h2>Sipariş oluştur</h2>
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
              Depo
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
            Not
            <textarea value={form.notes} onChange={(event) => setForm({ ...form, notes: event.target.value })} rows={3} />
          </label>
          <button className="primary-action" type="submit">
            <Plus size={17} />
            Sipariş Oluştur
          </button>
        </form>
      </section>

      <OperationTable
        title="Siparişler"
        icon={<ClipboardCheck size={19} />}
        rows={orders.map((order) => ({
          id: order.id,
          number: order.orderNumber,
          party: order.customerName,
          warehouse: order.warehouseName || "-",
          status: order.status,
          quantity: order.totalQuantity,
          date: order.createdAt
        }))}
      />
    </div>
  );
}