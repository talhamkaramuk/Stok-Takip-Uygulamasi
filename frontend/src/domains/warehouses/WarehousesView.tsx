import {
  ArrowLeftRight,
  Boxes,
  ClipboardList,
  Plus,
  Search
} from "lucide-react";
import type { FormEvent } from "react";
import { useState } from "react";
import type { ApiClient } from "../../shared/api/client";
import { getErrorMessage } from "../../shared/errors/getErrorMessage";
import { PaginationControls } from "../../shared/pagination/PaginationControls";
import { useServerPage } from "../../shared/pagination/useServerPage";
import type { Notice } from "../../shared/types/ui";
import type {
  Product,
  Warehouse,
  WarehouseStock
} from "../../types";

export function WarehousesView({
  api,
  products,
  warehouses,
  onChanged,
  setNotice
}: {
  api: ApiClient;
  products: Product[];
  warehouses: Warehouse[];
  onChanged: () => void;
  setNotice: (notice: Notice | null) => void;
}) {
  const [warehouseForm, setWarehouseForm] = useState({
    code: "",
    name: "",
    address: "",
    isDefault: false
  });
  const [transferForm, setTransferForm] = useState({
    productId: "",
    fromWarehouseId: "",
    toWarehouseId: "",
    quantity: 1,
    reason: ""
  });
  const [warehouseQuery, setWarehouseQuery] = useState("");
  const [warehouseActiveFilter, setWarehouseActiveFilter] = useState("");
  const [stockWarehouseId, setStockWarehouseId] = useState("");
  const [stockProductId, setStockProductId] = useState("");
  const activeWarehouses = warehouses.filter((warehouse) => warehouse.isActive);
  const warehousePage = useServerPage<Warehouse, { search?: string; isActive?: boolean }>({
    filters: {
      search: warehouseQuery.trim() || undefined,
      isActive: warehouseActiveFilter === "" ? undefined : warehouseActiveFilter === "true"
    },
    load: api.listWarehouses,
    onError: (error) => setNotice({ type: "error", message: getErrorMessage(error) })
  });
  const warehouseStockPage = useServerPage<WarehouseStock, { warehouseId?: string; productId?: string }>({
    filters: {
      warehouseId: stockWarehouseId || undefined,
      productId: stockProductId || undefined
    },
    load: api.listWarehouseStock,
    onError: (error) => setNotice({ type: "error", message: getErrorMessage(error) })
  });

  async function createWarehouse(event: FormEvent) {
    event.preventDefault();
    setNotice(null);
    try {
      await api.createWarehouse({
        code: warehouseForm.code,
        name: warehouseForm.name,
        address: warehouseForm.address.trim() || null,
        isDefault: warehouseForm.isDefault
      });
      setWarehouseForm({ code: "", name: "", address: "", isDefault: false });
      setNotice({ type: "success", message: "Depo oluşturuldu." });
      warehousePage.reload();
      onChanged();
    } catch (error) {
      setNotice({ type: "error", message: getErrorMessage(error) });
    }
  }

  async function transferStock(event: FormEvent) {
    event.preventDefault();
    setNotice(null);
    try {
      await api.transferStock({
        productId: transferForm.productId,
        fromWarehouseId: transferForm.fromWarehouseId,
        toWarehouseId: transferForm.toWarehouseId,
        quantity: transferForm.quantity,
        reason: transferForm.reason.trim() || null
      });
      setTransferForm({ productId: "", fromWarehouseId: "", toWarehouseId: "", quantity: 1, reason: "" });
      setNotice({ type: "success", message: "Depo transferi işlendi." });
      warehousePage.reload();
      warehouseStockPage.reload();
      onChanged();
    } catch (error) {
      setNotice({ type: "error", message: getErrorMessage(error) });
    }
  }

  return (
    <div className="content-grid">
      <div className="content-grid two-columns">
        <section className="tool-panel">
          <div className="section-title">
            <Boxes size={19} />
            <h2>Depo ekle</h2>
          </div>
          <form className="form-grid" onSubmit={createWarehouse}>
            <div className="inline-fields">
              <label>
                Kod
                <input value={warehouseForm.code} onChange={(event) => setWarehouseForm({ ...warehouseForm, code: event.target.value })} required />
              </label>
              <label>
                Depo adı
                <input value={warehouseForm.name} onChange={(event) => setWarehouseForm({ ...warehouseForm, name: event.target.value })} required />
              </label>
            </div>
            <label>
              Adres
              <textarea value={warehouseForm.address} onChange={(event) => setWarehouseForm({ ...warehouseForm, address: event.target.value })} rows={3} />
            </label>
            <label className="check-field">
              <input
                type="checkbox"
                checked={warehouseForm.isDefault}
                onChange={(event) => setWarehouseForm({ ...warehouseForm, isDefault: event.target.checked })}
              />
              Varsayılan depo yap
            </label>
            <button className="primary-action" type="submit">
              <Plus size={17} />
              Kaydet
            </button>
          </form>
        </section>

        <section className="tool-panel">
          <div className="section-title">
            <ArrowLeftRight size={19} />
            <h2>Depolar arası transfer</h2>
          </div>
          <form className="form-grid" onSubmit={transferStock}>
            <label>
              Ürün
              <select value={transferForm.productId} onChange={(event) => setTransferForm({ ...transferForm, productId: event.target.value })} required>
                <option value="">Seç</option>
                {products.map((product) => (
                  <option key={product.id} value={product.id}>
                    {product.sku} · {product.name}
                  </option>
                ))}
              </select>
            </label>
            <div className="inline-fields">
              <label>
                Kaynak depo
                <select value={transferForm.fromWarehouseId} onChange={(event) => setTransferForm({ ...transferForm, fromWarehouseId: event.target.value })} required>
                  <option value="">Seç</option>
                  {activeWarehouses.map((warehouse) => (
                    <option key={warehouse.id} value={warehouse.id}>
                      {warehouse.code} · {warehouse.name}
                    </option>
                  ))}
                </select>
              </label>
              <label>
                Hedef depo
                <select value={transferForm.toWarehouseId} onChange={(event) => setTransferForm({ ...transferForm, toWarehouseId: event.target.value })} required>
                  <option value="">Seç</option>
                  {activeWarehouses.map((warehouse) => (
                    <option key={warehouse.id} value={warehouse.id}>
                      {warehouse.code} · {warehouse.name}
                    </option>
                  ))}
                </select>
              </label>
            </div>
            <label>
              Miktar
              <input type="number" min={1} value={transferForm.quantity} onChange={(event) => setTransferForm({ ...transferForm, quantity: event.target.valueAsNumber })} />
            </label>
            <label>
              Açıklama
              <textarea value={transferForm.reason} onChange={(event) => setTransferForm({ ...transferForm, reason: event.target.value })} rows={3} />
            </label>
            <button className="primary-action" type="submit">
              <ArrowLeftRight size={17} />
              Transfer et
            </button>
          </form>
        </section>
      </div>

      <div className="content-grid two-columns">
        <section className="tool-panel">
          <div className="section-title spread">
            <span>
              <ClipboardList size={19} />
              <h2>Depolar</h2>
            </span>
            <div className="table-filter-row">
              <label className="search-field">
                <Search size={16} />
                <input value={warehouseQuery} onChange={(event) => setWarehouseQuery(event.target.value)} placeholder="Kod, depo veya adres ara" />
              </label>
              <select value={warehouseActiveFilter} onChange={(event) => setWarehouseActiveFilter(event.target.value)}>
                <option value="">Tüm durumlar</option>
                <option value="true">Aktif</option>
                <option value="false">Pasif</option>
              </select>
            </div>
          </div>
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Kod</th>
                  <th>Depo</th>
                  <th>Ürün</th>
                  <th>Stok</th>
                  <th>Durum</th>
                </tr>
              </thead>
              <tbody>
                {warehousePage.items.map((warehouse) => (
                  <tr key={warehouse.id}>
                    <td>{warehouse.code}</td>
                    <td>{warehouse.name}</td>
                    <td>{warehouse.productCount}</td>
                    <td>{warehouse.totalQuantity}</td>
                    <td>
                      <span className={warehouse.isActive ? "pill" : "pill warn"}>
                        {warehouse.isDefault ? "Varsayılan" : warehouse.isActive ? "Aktif" : "Pasif"}
                      </span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <PaginationControls
            page={warehousePage.page}
            totalPages={warehousePage.totalPages}
            totalCount={warehousePage.totalCount}
            startIndex={warehousePage.startIndex}
            endIndex={warehousePage.endIndex}
            onPageChange={warehousePage.setPage}
          />
        </section>

        <section className="tool-panel">
          <div className="section-title spread">
            <span>
              <Boxes size={19} />
              <h2>Depo stokları</h2>
            </span>
            <div className="table-filter-row">
              <select value={stockWarehouseId} onChange={(event) => setStockWarehouseId(event.target.value)}>
                <option value="">Tüm depolar</option>
                {activeWarehouses.map((warehouse) => (
                  <option key={warehouse.id} value={warehouse.id}>{warehouse.code} - {warehouse.name}</option>
                ))}
              </select>
              <select value={stockProductId} onChange={(event) => setStockProductId(event.target.value)}>
                <option value="">Tüm ürünler</option>
                {products.map((product) => (
                  <option key={product.id} value={product.id}>{product.sku} - {product.name}</option>
                ))}
              </select>
            </div>
          </div>
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Depo</th>
                  <th>SKU</th>
                  <th>Ürün</th>
                  <th>Stok</th>
                </tr>
              </thead>
              <tbody>
                {warehouseStockPage.items.map((item) => (
                  <tr key={`${item.warehouseId}-${item.productId}`}>
                    <td>{item.warehouseCode}</td>
                    <td>{item.sku}</td>
                    <td>{item.productName}</td>
                    <td>
                      <span className={item.isCritical ? "pill warn" : "pill"}>{item.quantity}</span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <PaginationControls
            page={warehouseStockPage.page}
            totalPages={warehouseStockPage.totalPages}
            totalCount={warehouseStockPage.totalCount}
            startIndex={warehouseStockPage.startIndex}
            endIndex={warehouseStockPage.endIndex}
            onPageChange={warehouseStockPage.setPage}
          />
        </section>
      </div>
    </div>
  );
}
