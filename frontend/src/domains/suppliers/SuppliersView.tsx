import {
  Check,
  ClipboardList,
  Handshake,
  Search
} from "lucide-react";
import type { FormEvent } from "react";
import { useState } from "react";
import type { ApiClient } from "../../shared/api/client";
import { getErrorMessage } from "../../shared/errors/getErrorMessage";
import { PaginationControls } from "../../shared/pagination/PaginationControls";
import { useServerPage } from "../../shared/pagination/useServerPage";
import type { Notice } from "../../shared/types/ui";
import { emptyToNull } from "../../shared/utils/inventory";
import type { Supplier } from "../../types";

const emptySupplierForm = {
  code: "",
  name: "",
  contactName: "",
  email: "",
  phone: "",
  taxNumber: "",
  address: "",
  notes: "",
  isActive: true
};

export function SuppliersView({
  api,
  onChanged,
  setNotice
}: {
  api: ApiClient;
  onChanged: () => void;
  setNotice: (notice: Notice | null) => void;
}) {
  const [editingId, setEditingId] = useState<string | null>(null);
  const [form, setForm] = useState(emptySupplierForm);
  const [query, setQuery] = useState("");
  const [activeFilter, setActiveFilter] = useState("");
  const supplierPage = useServerPage<Supplier, { search?: string; isActive?: boolean }>({
    filters: {
      search: query.trim() || undefined,
      isActive: activeFilter === "" ? undefined : activeFilter === "true"
    },
    load: api.listSuppliers,
    onError: (error) => setNotice({ type: "error", message: getErrorMessage(error) })
  });

  function edit(supplier: Supplier) {
    setEditingId(supplier.id);
    setForm({
      code: supplier.code,
      name: supplier.name,
      contactName: supplier.contactName ?? "",
      email: supplier.email ?? "",
      phone: supplier.phone ?? "",
      taxNumber: supplier.taxNumber ?? "",
      address: supplier.address ?? "",
      notes: supplier.notes ?? "",
      isActive: supplier.isActive
    });
  }

  function reset() {
    setEditingId(null);
    setForm(emptySupplierForm);
  }

  async function submit(event: FormEvent) {
    event.preventDefault();
    setNotice(null);
    const payload = {
      code: form.code,
      name: form.name,
      contactName: emptyToNull(form.contactName),
      email: emptyToNull(form.email),
      phone: emptyToNull(form.phone),
      taxNumber: emptyToNull(form.taxNumber),
      address: emptyToNull(form.address),
      notes: emptyToNull(form.notes)
    };

    try {
      if (editingId) {
        await api.updateSupplier(editingId, { ...payload, isActive: form.isActive });
        setNotice({ type: "success", message: "Tedarikçi güncellendi." });
      } else {
        await api.createSupplier(payload);
        setNotice({ type: "success", message: "Tedarikçi kaydedildi." });
      }
      reset();
      supplierPage.reload();
      onChanged();
    } catch (error) {
      setNotice({ type: "error", message: getErrorMessage(error) });
    }
  }

  async function deactivate(id: string) {
    setNotice(null);
    try {
      await api.deactivateSupplier(id);
      setNotice({ type: "success", message: "Tedarikçi pasife alındı." });
      if (editingId === id) {
        reset();
      }
      supplierPage.reload();
      onChanged();
    } catch (error) {
      setNotice({ type: "error", message: getErrorMessage(error) });
    }
  }

  return (
    <div className="content-grid two-columns">
      <section className="tool-panel">
        <div className="section-title">
          <Handshake size={19} />
          <h2>{editingId ? "Tedarikçi düzenle" : "Tedarikçi ekle"}</h2>
        </div>
        <form className="form-grid" onSubmit={submit}>
          <div className="inline-fields">
            <label>
              Kod
              <input value={form.code} onChange={(event) => setForm({ ...form, code: event.target.value })} required />
            </label>
            <label>
              Unvan
              <input value={form.name} onChange={(event) => setForm({ ...form, name: event.target.value })} required />
            </label>
          </div>
          <div className="inline-fields">
            <label>
              Yetkili
              <input value={form.contactName} onChange={(event) => setForm({ ...form, contactName: event.target.value })} />
            </label>
            <label>
              Telefon
              <input value={form.phone} onChange={(event) => setForm({ ...form, phone: event.target.value })} />
            </label>
          </div>
          <div className="inline-fields">
            <label>
              E-posta
              <input type="email" value={form.email} onChange={(event) => setForm({ ...form, email: event.target.value })} />
            </label>
            <label>
              Vergi No
              <input value={form.taxNumber} onChange={(event) => setForm({ ...form, taxNumber: event.target.value })} />
            </label>
          </div>
          <label>
            Adres
            <textarea value={form.address} onChange={(event) => setForm({ ...form, address: event.target.value })} rows={2} />
          </label>
          <label>
            Not
            <textarea value={form.notes} onChange={(event) => setForm({ ...form, notes: event.target.value })} rows={2} />
          </label>
          {editingId && (
            <label className="check-row">
              <input type="checkbox" checked={form.isActive} onChange={(event) => setForm({ ...form, isActive: event.target.checked })} />
              Aktif tedarikçi
            </label>
          )}
          <div className="button-row">
            {editingId && (
              <button className="ghost-action" type="button" onClick={reset}>
                Vazgeç
              </button>
            )}
            <button className="primary-action" type="submit">
              <Check size={17} />
              {editingId ? "Güncelle" : "Kaydet"}
            </button>
          </div>
        </form>
      </section>

      <section className="tool-panel">
        <div className="section-title spread">
          <span>
            <ClipboardList size={19} />
            <h2>Tedarikçiler</h2>
          </span>
          <div className="table-filter-row">
            <label className="search-field">
              <Search size={16} />
              <input value={query} onChange={(event) => setQuery(event.target.value)} placeholder="Kod, unvan veya iletişim ara" />
            </label>
            <select value={activeFilter} onChange={(event) => setActiveFilter(event.target.value)}>
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
                <th>Unvan</th>
                <th>İletişim</th>
                <th>Durum</th>
                <th>İşlem</th>
              </tr>
            </thead>
            <tbody>
              {supplierPage.items.map((supplier) => (
                <tr key={supplier.id}>
                  <td>{supplier.code}</td>
                  <td>{supplier.name}</td>
                  <td>{supplier.phone || supplier.email || "-"}</td>
                  <td><span className={supplier.isActive ? "pill" : "pill warn"}>{supplier.isActive ? "Aktif" : "Pasif"}</span></td>
                  <td>
                    <div className="table-actions">
                      <button className="ghost-action compact-action" type="button" onClick={() => edit(supplier)}>Düzenle</button>
                      {supplier.isActive && (
                        <button className="ghost-action compact-action" type="button" onClick={() => void deactivate(supplier.id)}>Pasifle</button>
                      )}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <PaginationControls
          page={supplierPage.page}
          totalPages={supplierPage.totalPages}
          totalCount={supplierPage.totalCount}
          startIndex={supplierPage.startIndex}
          endIndex={supplierPage.endIndex}
          onPageChange={supplierPage.setPage}
        />
      </section>
    </div>
  );
}
