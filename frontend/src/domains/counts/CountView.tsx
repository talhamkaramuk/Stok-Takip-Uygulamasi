import {
  Check,
  ClipboardList,
  Plus,
  RefreshCw,
  ScanLine
} from "lucide-react";
import type { FormEvent } from "react";
import { useEffect, useMemo, useState } from "react";
import type { ApiClient } from "../../shared/api/client";
import { getErrorMessage } from "../../shared/errors/getErrorMessage";
import { PaginationControls } from "../../shared/pagination/PaginationControls";
import { usePagination } from "../../shared/pagination/usePagination";
import type { Notice } from "../../shared/types/ui";
import { BarcodeScanner } from "../../shared/ui/BarcodeScanner";
import { findProductByBarcode, formatDate, getDefaultWarehouseId, isActiveWarehouseId } from "../../shared/utils/inventory";
import type {
  CountDifference,
  InventoryCount,
  InventoryCountItem,
  Product,
  Warehouse
} from "../../types";

export function CountView({
  api,
  products,
  warehouses,
  activeCount,
  setActiveCount,
  lastScannedItem,
  setLastScannedItem,
  differences,
  loadDifferences,
  onChanged,
  setNotice
}: {
  api: ApiClient;
  products: Product[];
  warehouses: Warehouse[];
  activeCount: InventoryCount | null;
  setActiveCount: (count: InventoryCount | null) => void;
  lastScannedItem: InventoryCountItem | null;
  setLastScannedItem: (item: InventoryCountItem | null) => void;
  differences: CountDifference[];
  loadDifferences: (countId: string) => Promise<void>;
  onChanged: () => void;
  setNotice: (notice: Notice | null) => void;
}) {
  const [countName, setCountName] = useState(`Sayım ${new Date().toLocaleDateString("tr-TR")}`);
  const [warehouseId, setWarehouseId] = useState("");
  const [barcode, setBarcode] = useState("");
  const [quantity, setQuantity] = useState(1);
  const activeWarehouses = useMemo(() => warehouses.filter((warehouse) => warehouse.isActive), [warehouses]);
  const defaultWarehouseId = useMemo(() => getDefaultWarehouseId(activeWarehouses), [activeWarehouses]);
  const selectedWarehouseId = isActiveWarehouseId(activeWarehouses, warehouseId) ? warehouseId : defaultWarehouseId;
  const productsWithBarcodes = useMemo(
    () => products.filter((product) => product.isActive && product.barcodes.length > 0),
    [products]);
  const differencePagination = usePagination(differences);

  useEffect(() => {
    if (warehouseId && !isActiveWarehouseId(activeWarehouses, warehouseId)) {
      setWarehouseId(defaultWarehouseId);
    }
  }, [activeWarehouses, defaultWarehouseId, warehouseId]);

  async function createCount(event: FormEvent) {
    event.preventDefault();
    setNotice(null);
    try {
      const count = await api.createCount(countName, selectedWarehouseId || null);
      setActiveCount(count);
      setLastScannedItem(null);
      setNotice({ type: "success", message: "Sayım başlatıldı." });
    } catch (error) {
      setNotice({ type: "error", message: getErrorMessage(error) });
    }
  }

  async function scan(value: string) {
    if (!activeCount) {
      setNotice({ type: "error", message: "Açık sayım bulunamadı." });
      return;
    }

    const normalizedBarcode = value.trim();
    if (!normalizedBarcode) {
      setNotice({ type: "error", message: "Barkod boş olamaz." });
      return;
    }

    const product = findProductByBarcode(products, normalizedBarcode);
    if (!product) {
      setNotice({
        type: "error",
        message: "Bu barkod aktif bir ürüne tanımlı değil. Ürünler sayfasında barkodu ürüne ekleyin veya aşağıdaki tanımlı barkodlardan birini kullanın."
      });
      return;
    }

    setNotice(null);
    try {
      const item = await api.scanCountItem(activeCount.id, { barcode: normalizedBarcode, quantity });
      setLastScannedItem(item);
      setBarcode("");
      await loadDifferences(activeCount.id);
      setActiveCount(await api.getCount(activeCount.id));
    } catch (error) {
      setNotice({ type: "error", message: getErrorMessage(error) });
    }
  }

  async function submitScan(event: FormEvent) {
    event.preventDefault();
    await scan(barcode);
  }

  async function closeCount(applyDifferences: boolean) {
    if (!activeCount) {
      return;
    }

    try {
      const count = await api.closeCount(activeCount.id, applyDifferences);
      setActiveCount(count);
      await loadDifferences(count.id);
      onChanged();
      setNotice({ type: "success", message: "Sayım kapatıldı." });
    } catch (error) {
      setNotice({ type: "error", message: getErrorMessage(error) });
    }
  }

  return (
    <div className="content-grid two-columns">
      <section className="tool-panel">
        <div className="section-title">
          <ScanLine size={19} />
          <h2>Sayım</h2>
        </div>

        {!activeCount || activeCount.status !== "Open" ? (
          <form className="form-grid" onSubmit={createCount}>
            <label>
              Sayım adı
              <input value={countName} onChange={(event) => setCountName(event.target.value)} />
            </label>
            <label>
              Depo
              <select value={selectedWarehouseId} onChange={(event) => setWarehouseId(event.target.value)}>
                <option value="">Varsayılan depo</option>
                {activeWarehouses.map((warehouse) => (
                  <option key={warehouse.id} value={warehouse.id}>
                    {warehouse.code} · {warehouse.name}
                  </option>
                ))}
              </select>
            </label>
            <button className="primary-action" type="submit">
              <Plus size={17} />
              Başlat
            </button>
          </form>
        ) : (
          <>
            <div className="count-status">
              <strong>{activeCount.name}</strong>
              <div className="status-grid">
                <span><b>Depo</b>{activeCount.warehouseName || "Varsayılan depo"}</span>
                <span><b>Ürün</b>{activeCount.itemCount}</span>
                <span><b>Fark</b>{activeCount.differenceCount}</span>
              </div>
              {activeCount.hasPostSnapshotMovements && (
                <span className="inline-warning">
                  Sayım başlangıcından sonra bu depoda {activeCount.postSnapshotMovementCount} stok hareketi oluştu
                  {activeCount.lastPostSnapshotMovementAt ? `; son hareket: ${formatDate(activeCount.lastPostSnapshotMovementAt)}` : ""}.
                  Farkları snapshot başlangıcına göre yorumlayın.
                </span>
              )}
            </div>

            <BarcodeScanner onDetect={(value) => void scan(value)} />

            <form className="form-grid" onSubmit={submitScan}>
              <label>
                Barkod
                <input value={barcode} onChange={(event) => setBarcode(event.target.value)} />
              </label>
              <label>
                Adet
                <input type="number" min={1} value={quantity} onChange={(event) => setQuantity(event.target.valueAsNumber)} />
              </label>
              <button className="primary-action" type="submit">
                <ScanLine size={17} />
                Say
              </button>
            </form>

            <div className="barcode-list">
              <div className="mini-heading">
                <strong>Tanımlı barkodlar</strong>
                <span>{productsWithBarcodes.length}</span>
              </div>
              {productsWithBarcodes.length === 0 ? (
                <p className="empty-note">Aktif ürünlerde barkod tanımı yok.</p>
              ) : (
                <div className="barcode-buttons">
                  {productsWithBarcodes.slice(0, 8).map((product) => (
                    <button
                      className="ghost-action barcode-choice"
                      key={product.id}
                      type="button"
                      onClick={() => setBarcode(product.barcodes[0])}
                    >
                      <span>{product.sku}</span>
                      <strong>{product.barcodes[0]}</strong>
                    </button>
                  ))}
                </div>
              )}
            </div>

            <div className="button-row">
              <button className="ghost-action" type="button" onClick={() => void closeCount(false)}>
                <Check size={17} />
                Kapat
              </button>
              <button className="primary-action" type="button" onClick={() => void closeCount(true)}>
                <RefreshCw size={17} />
                Farkları uygula
              </button>
            </div>
          </>
        )}
      </section>

      <section className="tool-panel">
        <div className="section-title">
          <ClipboardList size={19} />
          <h2>Sayım farkları</h2>
        </div>
        {lastScannedItem && (
          <article className="scan-result">
            <strong>{lastScannedItem.sku} · {lastScannedItem.productName}</strong>
            <span>Beklenen {lastScannedItem.expectedQuantity} · Sayılan {lastScannedItem.countedQuantity}</span>
          </article>
        )}
        <div className="table-wrap compact-table">
          <table>
            <thead>
              <tr>
                <th>Ürün</th>
                <th>Beklenen</th>
                <th>Sayılan</th>
                <th>Fark</th>
              </tr>
            </thead>
            <tbody>
              {differencePagination.items.map((item) => (
                <tr key={item.productId}>
                  <td>{item.sku}</td>
                  <td>{item.expectedQuantity}</td>
                  <td>{item.countedQuantity}</td>
                  <td>
                    <span className={item.difference === 0 ? "pill" : "pill warn"}>{item.difference}</span>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <PaginationControls
          page={differencePagination.page}
          totalPages={differencePagination.totalPages}
          totalCount={differencePagination.totalCount}
          startIndex={differencePagination.startIndex}
          endIndex={differencePagination.endIndex}
          onPageChange={differencePagination.setPage}
        />
      </section>
    </div>
  );
}