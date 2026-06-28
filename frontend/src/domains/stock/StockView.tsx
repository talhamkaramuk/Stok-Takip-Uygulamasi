import {
  Boxes,
  Check,
  ClipboardList
} from "lucide-react";
import type { FormEvent } from "react";
import { useEffect, useMemo, useState } from "react";
import type { ApiClient } from "../../shared/api/client";
import { getErrorMessage } from "../../shared/errors/getErrorMessage";
import type { Notice } from "../../shared/types/ui";
import { getDefaultWarehouseId, isActiveWarehouseId } from "../../shared/utils/inventory";
import type {
  Product,
  StockMovement,
  StockMovementType,
  Warehouse
} from "../../types";

export function StockView({
  api,
  products,
  warehouses,
  movements,
  onChanged,
  setNotice
}: {
  api: ApiClient;
  products: Product[];
  warehouses: Warehouse[];
  movements: StockMovement[];
  onChanged: () => void;
  setNotice: (notice: Notice | null) => void;
}) {
  const [form, setForm] = useState({
    productId: "",
    warehouseId: "",
    type: "In" as StockMovementType,
    quantity: 1,
    reason: ""
  });
  const activeWarehouses = useMemo(() => warehouses.filter((warehouse) => warehouse.isActive), [warehouses]);
  const defaultWarehouseId = useMemo(() => getDefaultWarehouseId(activeWarehouses), [activeWarehouses]);
  const selectedWarehouseId = isActiveWarehouseId(activeWarehouses, form.warehouseId) ? form.warehouseId : defaultWarehouseId;

  useEffect(() => {
    if (form.warehouseId && !isActiveWarehouseId(activeWarehouses, form.warehouseId)) {
      setForm((current) => ({ ...current, warehouseId: defaultWarehouseId }));
    }
  }, [activeWarehouses, defaultWarehouseId, form.warehouseId]);

  async function submit(event: FormEvent) {
    event.preventDefault();
    setNotice(null);
    try {
      await api.createStockMovement({
        productId: form.productId,
        warehouseId: selectedWarehouseId || null,
        type: form.type,
        quantity: form.quantity,
        reason: form.reason.trim() || null
      });
      setForm({ productId: "", warehouseId: "", type: "In", quantity: 1, reason: "" });
      setNotice({ type: "success", message: "Stok hareketi işlendi." });
      onChanged();
    } catch (error) {
      setNotice({ type: "error", message: getErrorMessage(error) });
    }
  }

  return (
    <div className="content-grid two-columns">
      <section className="tool-panel">
        <div className="section-title">
          <Boxes size={19} />
          <h2>Stok hareketi</h2>
        </div>
        <form className="form-grid" onSubmit={submit}>
          <label>
            Ürün
            <select value={form.productId} onChange={(event) => setForm({ ...form, productId: event.target.value })} required>
              <option value="">Seç</option>
              {products.map((product) => (
                <option key={product.id} value={product.id}>
                  {product.sku} · {product.name}
                </option>
              ))}
            </select>
          </label>
          <label>
            Depo
            <select value={selectedWarehouseId} onChange={(event) => setForm({ ...form, warehouseId: event.target.value })}>
              <option value="">Varsayılan depo</option>
              {activeWarehouses.map((warehouse) => (
                <option key={warehouse.id} value={warehouse.id}>
                  {warehouse.code} · {warehouse.name}
                </option>
              ))}
            </select>
          </label>
          <div className="segmented-control" role="radiogroup" aria-label="Hareket tipi">
            {(["In", "Out", "Adjustment", "CountCorrection"] as StockMovementType[]).map((type) => (
              <button
                key={type}
                className={form.type === type ? "active" : ""}
                type="button"
                onClick={() => setForm({ ...form, type })}
              >
                {type === "In" ? "Giriş" : type === "Out" ? "Çıkış" : type === "Adjustment" ? "Düzeltme" : "Sayım"}
              </button>
            ))}
          </div>
          <label>
            Miktar
            <input type="number" min={0} value={form.quantity} onChange={(event) => setForm({ ...form, quantity: event.target.valueAsNumber })} />
          </label>
          <label>
            Açıklama
            <textarea value={form.reason} onChange={(event) => setForm({ ...form, reason: event.target.value })} rows={3} />
          </label>
          <button className="primary-action" type="submit">
            <Check size={17} />
            İşle
          </button>
        </form>
      </section>

      <section className="tool-panel">
        <div className="section-title">
          <ClipboardList size={19} />
          <h2>Hareket geçmişi</h2>
        </div>
        <div className="timeline-list">
          {movements.slice(0, 12).map((movement) => (
            <article className="timeline-item" key={movement.id}>
              <span className={`movement-dot ${movement.type.toLowerCase()}`} />
              <div>
                <strong>{movement.sku} · {movement.productName}</strong>
                <p>{movement.warehouseName || "Depo"} · {movement.type} · {movement.previousQuantity} → {movement.newQuantity}</p>
              </div>
              <time>{new Date(movement.createdAt).toLocaleString("tr-TR")}</time>
            </article>
          ))}
        </div>
      </section>
    </div>
  );
}