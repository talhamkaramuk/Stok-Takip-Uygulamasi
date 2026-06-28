import {
  Building2,
  Check,
  ClipboardList
} from "lucide-react";
import type { FormEvent } from "react";
import { useState } from "react";
import type { ApiClient } from "../../shared/api/client";
import { getErrorMessage } from "../../shared/errors/getErrorMessage";
import { PaginationControls } from "../../shared/pagination/PaginationControls";
import { usePagination } from "../../shared/pagination/usePagination";
import type { Notice } from "../../shared/types/ui";
import { emptyToNull } from "../../shared/utils/inventory";
import type {
  Customer
} from "../../types";

const emptyCustomerForm = {
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

export function CustomersView({
  api,
  customers,
  onChanged,
  setNotice
}: {
  api: ApiClient;
  customers: Customer[];
  onChanged: () => void;
  setNotice: (notice: Notice | null) => void;
}) {
  const [editingId, setEditingId] = useState<string | null>(null);
  const [form, setForm] = useState(emptyCustomerForm);
  const customerPagination = usePagination(customers);

  function edit(customer: Customer) {
    setEditingId(customer.id);
    setForm({
      code: customer.code,
      name: customer.name,
      contactName: customer.contactName ?? "",
      email: customer.email ?? "",
      phone: customer.phone ?? "",
      taxNumber: customer.taxNumber ?? "",
      address: customer.address ?? "",
      notes: customer.notes ?? "",
      isActive: customer.isActive
    });
  }

  function reset() {
    setEditingId(null);
    setForm(emptyCustomerForm);
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
        await api.updateCustomer(editingId, { ...payload, isActive: form.isActive });
        setNotice({ type: "success", message: "Müşteri güncellendi." });
      } else {
        await api.createCustomer(payload);
        setNotice({ type: "success", message: "Müşteri kaydedildi." });
      }
      reset();
      onChanged();
    } catch (error) {
      setNotice({ type: "error", message: getErrorMessage(error) });
    }
  }

  async function deactivate(id: string) {
    setNotice(null);
    try {
      await api.deactivateCustomer(id);
      setNotice({ type: "success", message: "Müşteri pasife alındı." });
      if (editingId === id) {
        reset();
      }
      onChanged();
    } catch (error) {
      setNotice({ type: "error", message: getErrorMessage(error) });
    }
  }

  return (
    <div className="content-grid two-columns">
      <section className="tool-panel">
        <div className="section-title">
          <Building2 size={19} />
          <h2>{editingId ? "Müşteri düzenle" : "Müşteri ekle"}</h2>
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
              Aktif müşteri
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
        <div className="section-title">
          <ClipboardList size={19} />
          <h2>Müşteriler</h2>
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
              {customerPagination.items.map((customer) => (
                <tr key={customer.id}>
                  <td>{customer.code}</td>
                  <td>{customer.name}</td>
                  <td>{customer.phone || customer.email || "-"}</td>
                  <td><span className={customer.isActive ? "pill" : "pill warn"}>{customer.isActive ? "Aktif" : "Pasif"}</span></td>
                  <td>
                    <div className="table-actions">
                      <button className="ghost-action compact-action" type="button" onClick={() => edit(customer)}>Düzenle</button>
                      {customer.isActive && (
                        <button className="ghost-action compact-action" type="button" onClick={() => void deactivate(customer.id)}>Pasifle</button>
                      )}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <PaginationControls
          page={customerPagination.page}
          totalPages={customerPagination.totalPages}
          totalCount={customerPagination.totalCount}
          startIndex={customerPagination.startIndex}
          endIndex={customerPagination.endIndex}
          onPageChange={customerPagination.setPage}
        />
      </section>
    </div>
  );
}

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
