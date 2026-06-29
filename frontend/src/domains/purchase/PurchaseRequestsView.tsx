import {
  Boxes,
  ClipboardList,
  Download,
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
import { getDefaultWarehouseId, statusClass, statusLabel } from "../../shared/utils/inventory";
import type {
  OperationItem,
  Product,
  PurchaseRequest,
  PurchaseRequestStatus,
  Supplier,
  Warehouse
} from "../../types";

export function PurchaseRequestsView({
  api,
  products,
  warehouses,
  suppliers,
  onChanged,
  setNotice
}: {
  api: ApiClient;
  products: Product[];
  warehouses: Warehouse[];
  suppliers: Supplier[];
  onChanged: () => void;
  setNotice: (notice: Notice | null) => void;
}) {
  const [form, setForm] = useState({ supplierId: "", supplierName: "", productId: "", warehouseId: "", quantity: 1, notes: "" });
  const [receiveForm, setReceiveForm] = useState({ requestId: "", productId: "", quantity: 1 });
  const [query, setQuery] = useState("");
  const [status, setStatus] = useState<PurchaseRequestStatus | "">("");
  const activeWarehouses = warehouses.filter((warehouse) => warehouse.isActive);
  const activeSuppliers = suppliers.filter((supplier) => supplier.isActive);
  const selectedWarehouseId = form.warehouseId || getDefaultWarehouseId(activeWarehouses);
  const requestPage = useServerPage<PurchaseRequest, { search?: string; status?: PurchaseRequestStatus }>({
    filters: {
      search: query.trim() || undefined,
      status: status || undefined
    },
    load: api.listPurchaseRequests,
    onError: (error) => setNotice({ type: "error", message: getErrorMessage(error) })
  });
  const receivableRequests = requestPage.items.filter((request) =>
    (request.status === "Approved" || request.status === "PartiallyReceived") &&
    request.items.some((item) => item.quantity - (item.receivedQuantity ?? 0) > 0));
  const selectedReceiveRequest = requestPage.items.find((request) => request.id === receiveForm.requestId);
  const receivableItems = selectedReceiveRequest?.items.filter((item) => item.quantity - (item.receivedQuantity ?? 0) > 0) ?? [];

  function selectSupplier(supplierId: string) {
    const supplier = activeSuppliers.find((item) => item.id === supplierId);
    setForm({ ...form, supplierId, supplierName: supplier?.name ?? form.supplierName });
  }

  function remainingPurchaseQuantity(item: OperationItem) {
    return item.quantity - (item.receivedQuantity ?? 0);
  }

  function selectReceiveRequest(requestId: string) {
    const request = requestPage.items.find((item) => item.id === requestId);
    const item = request?.items.find((line) => remainingPurchaseQuantity(line) > 0);
    setReceiveForm({
      requestId,
      productId: item?.productId ?? "",
      quantity: item ? remainingPurchaseQuantity(item) : 1
    });
  }

  function selectReceiveProduct(productId: string) {
    const item = receivableItems.find((line) => line.productId === productId);
    setReceiveForm({
      ...receiveForm,
      productId,
      quantity: item ? remainingPurchaseQuantity(item) : receiveForm.quantity
    });
  }

  async function submit(event: FormEvent) {
    event.preventDefault();
    setNotice(null);
    try {
      await api.createPurchaseRequest({
        supplierId: form.supplierId || null,
        supplierName: form.supplierName,
        warehouseId: selectedWarehouseId || null,
        notes: form.notes.trim() || null,
        items: [{ productId: form.productId, quantity: form.quantity }]
      });
      setForm({ supplierId: "", supplierName: "", productId: "", warehouseId: "", quantity: 1, notes: "" });
      setNotice({ type: "success", message: "Alım talebi oluşturuldu." });
      requestPage.reload();
      onChanged();
    } catch (error) {
      setNotice({ type: "error", message: getErrorMessage(error) });
    }
  }

  async function mutate(id: string, action: "approve" | "receive") {
    setNotice(null);
    try {
      if (action === "approve") {
        await api.approvePurchaseRequest(id);
        setNotice({ type: "success", message: "Alım talebi onaylandı." });
      } else {
        await api.receivePurchaseRequest(id);
        setNotice({ type: "success", message: "Alım talebi teslim alındı ve stok işlendi." });
      }
      requestPage.reload();
      onChanged();
    } catch (error) {
      setNotice({ type: "error", message: getErrorMessage(error) });
    }
  }

  async function submitReceive(event: FormEvent) {
    event.preventDefault();
    setNotice(null);
    try {
      await api.receivePurchaseRequest(receiveForm.requestId, {
        items: [{ productId: receiveForm.productId, quantity: receiveForm.quantity }]
      });
      setReceiveForm({ requestId: "", productId: "", quantity: 1 });
      setNotice({ type: "success", message: "Kısmi teslimat alındı ve stok işlendi." });
      requestPage.reload();
      onChanged();
    } catch (error) {
      setNotice({ type: "error", message: getErrorMessage(error) });
    }
  }

  return (
    <div className="content-grid two-columns">
      <section className="tool-panel">
        <div className="section-title">
          <Download size={19} />
          <h2>Alım talebi oluştur</h2>
        </div>
        <form className="form-grid" onSubmit={submit}>
          <label>
            Kayıtlı tedarikçi
            <select value={form.supplierId} onChange={(event) => selectSupplier(event.target.value)}>
              <option value="">Serbest tedarikçi</option>
              {activeSuppliers.map((supplier) => (
                <option key={supplier.id} value={supplier.id}>{supplier.code} - {supplier.name}</option>
              ))}
            </select>
          </label>
          <label>
            Tedarikçi
            <input value={form.supplierName} onChange={(event) => setForm({ ...form, supplierName: event.target.value })} required />
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
              Teslim Deposu
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
            Talep Oluştur
          </button>
        </form>
      </section>

      <section className="tool-panel">
        <div className="section-title spread">
          <span>
            <ClipboardList size={19} />
            <h2>Alım talepleri</h2>
          </span>
          <div className="table-filter-row">
            <label className="search-field">
              <Search size={16} />
              <input value={query} onChange={(event) => setQuery(event.target.value)} placeholder="Talep no, tedarikçi veya depo ara" />
            </label>
            <select value={status} onChange={(event) => setStatus(event.target.value as PurchaseRequestStatus | "")}>
              <option value="">Tüm durumlar</option>
              <option value="PendingApproval">Onay bekliyor</option>
              <option value="Approved">Onaylandı</option>
              <option value="PartiallyReceived">Kısmi teslim</option>
              <option value="Received">Teslim alındı</option>
              <option value="Cancelled">İptal</option>
            </select>
          </div>
        </div>
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Talep No</th>
                <th>Tedarikçi</th>
                <th>Depo</th>
                <th>Teslim / Talep</th>
                <th>Durum</th>
                <th>İşlem</th>
              </tr>
            </thead>
            <tbody>
              {requestPage.items.map((request) => (
                <tr key={request.id}>
                  <td>{request.requestNumber}</td>
                  <td>{request.supplierName}</td>
                  <td>{request.warehouseName || "-"}</td>
                  <td>{request.items.reduce((sum, item) => sum + (item.receivedQuantity ?? 0), 0)} / {request.totalQuantity}</td>
                  <td><span className={statusClass(request.status)}>{statusLabel(request.status)}</span></td>
                  <td>
                    <div className="table-actions">
                      {request.status === "PendingApproval" && (
                        <button className="ghost-action compact-action" type="button" onClick={() => void mutate(request.id, "approve")}>Onayla</button>
                      )}
                      {(request.status === "Approved" || request.status === "PartiallyReceived") && (
                        <button className="ghost-action compact-action" type="button" onClick={() => selectReceiveRequest(request.id)}>Kısmi Al</button>
                      )}
                      {(request.status === "Approved" || request.status === "PartiallyReceived") && (
                        <button className="primary-action compact-action" type="button" onClick={() => void mutate(request.id, "receive")}>Kalanı Al</button>
                      )}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        {receivableRequests.length > 0 && (
          <form className="form-grid" onSubmit={submitReceive}>
            <div className="inline-fields">
              <label>
                Kısmi teslim talebi
                <select value={receiveForm.requestId} onChange={(event) => selectReceiveRequest(event.target.value)} required>
                  <option value="">Seç</option>
                  {receivableRequests.map((request) => (
                    <option key={request.id} value={request.id}>{request.requestNumber} - {request.supplierName}</option>
                  ))}
                </select>
              </label>
              <label>
                Ürün
                <select value={receiveForm.productId} onChange={(event) => selectReceiveProduct(event.target.value)} required>
                  <option value="">Seç</option>
                  {receivableItems.map((item) => (
                    <option key={item.productId} value={item.productId}>{item.sku} - kalan {remainingPurchaseQuantity(item)}</option>
                  ))}
                </select>
              </label>
            </div>
            <div className="inline-fields">
              <label>
                Teslim adet
                <input
                  type="number"
                  min={1}
                  max={receivableItems.find((item) => item.productId === receiveForm.productId) ? remainingPurchaseQuantity(receivableItems.find((item) => item.productId === receiveForm.productId)!) : undefined}
                  value={receiveForm.quantity}
                  onChange={(event) => setReceiveForm({ ...receiveForm, quantity: event.target.valueAsNumber })}
                  required
                />
              </label>
              <button className="primary-action" type="submit">
                <Boxes size={17} />
                Kısmi Al
              </button>
            </div>
          </form>
        )}
        <PaginationControls
          page={requestPage.page}
          totalPages={requestPage.totalPages}
          totalCount={requestPage.totalCount}
          startIndex={requestPage.startIndex}
          endIndex={requestPage.endIndex}
          onPageChange={requestPage.setPage}
        />
      </section>
    </div>
  );
}
