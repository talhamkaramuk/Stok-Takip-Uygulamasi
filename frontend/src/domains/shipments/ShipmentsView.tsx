import {
  Truck
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
  Shipment,
  Warehouse
} from "../../types";

export function ShipmentsView({
  api,
  products,
  warehouses,
  customers,
  orders,
  shipments,
  onChanged,
  setNotice
}: {
  api: ApiClient;
  products: Product[];
  warehouses: Warehouse[];
  customers: Customer[];
  orders: SalesOrder[];
  shipments: Shipment[];
  onChanged: () => void;
  setNotice: (notice: Notice | null) => void;
}) {
  const [form, setForm] = useState({ salesOrderId: "", customerId: "", recipientName: "", productId: "", warehouseId: "", quantity: 1, trackingNumber: "", notes: "" });
  const activeWarehouses = warehouses.filter((warehouse) => warehouse.isActive);
  const activeCustomers = customers.filter((customer) => customer.isActive);
  const shippableOrders = orders.filter((order) =>
    order.status !== "Draft" &&
    order.status !== "Cancelled" &&
    order.items.some((item) => item.quantity - (item.shippedQuantity ?? 0) > 0));
  const selectedWarehouseId = form.warehouseId || getDefaultWarehouseId(activeWarehouses);

  function selectOrder(orderId: string) {
    const order = orders.find((item) => item.id === orderId);
    const orderItem = order?.items.find((item) => item.quantity - (item.shippedQuantity ?? 0) > 0);
    setForm({
      ...form,
      salesOrderId: orderId,
      customerId: order?.customerId ?? form.customerId,
      recipientName: order?.customerName ?? form.recipientName,
      warehouseId: order?.warehouseId ?? form.warehouseId,
      productId: orderItem?.productId ?? form.productId,
      quantity: orderItem ? orderItem.quantity - (orderItem.shippedQuantity ?? 0) : form.quantity
    });
  }

  function selectCustomer(customerId: string) {
    const customer = activeCustomers.find((item) => item.id === customerId);
    setForm({ ...form, customerId, recipientName: customer?.name ?? form.recipientName });
  }

  async function submit(event: FormEvent) {
    event.preventDefault();
    setNotice(null);
    try {
      await api.createShipment({
        salesOrderId: form.salesOrderId || null,
        customerId: form.customerId || null,
        recipientName: form.recipientName,
        warehouseId: selectedWarehouseId || null,
        trackingNumber: form.trackingNumber.trim() || null,
        notes: form.notes.trim() || null,
        items: [{ productId: form.productId, quantity: form.quantity }]
      });
      setForm({ salesOrderId: "", customerId: "", recipientName: "", productId: "", warehouseId: "", quantity: 1, trackingNumber: "", notes: "" });
      setNotice({ type: "success", message: "Sevkiyat oluşturuldu ve stok çıkışı işlendi." });
      onChanged();
    } catch (error) {
      setNotice({ type: "error", message: getErrorMessage(error) });
    }
  }

  return (
    <div className="content-grid two-columns">
      <section className="tool-panel">
        <div className="section-title">
          <Truck size={19} />
          <h2>Sevkiyat oluştur</h2>
        </div>
        <form className="form-grid" onSubmit={submit}>
          <label>
            Kayıtlı müşteri
            <select value={form.customerId} onChange={(event) => selectCustomer(event.target.value)}>
              <option value="">Serbest alıcı</option>
              {activeCustomers.map((customer) => (
                <option key={customer.id} value={customer.id}>{customer.code} - {customer.name}</option>
              ))}
            </select>
          </label>
          <label>
            Bağlı sipariş
            <select value={form.salesOrderId} onChange={(event) => selectOrder(event.target.value)}>
              <option value="">Bağımsız sevkiyat</option>
              {shippableOrders.map((order) => (
                <option key={order.id} value={order.id}>{order.orderNumber} - {order.customerName}</option>
              ))}
            </select>
          </label>
          <label>
            Alıcı
            <input value={form.recipientName} onChange={(event) => setForm({ ...form, recipientName: event.target.value })} required />
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
              Çıkış Deposu
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
            Takip No
            <input value={form.trackingNumber} onChange={(event) => setForm({ ...form, trackingNumber: event.target.value })} />
          </label>
          <button className="primary-action" type="submit">
            <Truck size={17} />
            Sevkiyat Oluştur
          </button>
        </form>
      </section>

      <OperationTable
        title="Sevkiyatlar"
        icon={<Truck size={19} />}
        rows={shipments.map((shipment) => ({
          id: shipment.id,
          number: shipment.shipmentNumber,
          party: shipment.recipientName,
          warehouse: shipment.warehouseName || "-",
          status: shipment.status,
          quantity: shipment.totalQuantity,
          date: shipment.shippedAt
        }))}
      />
    </div>
  );
}