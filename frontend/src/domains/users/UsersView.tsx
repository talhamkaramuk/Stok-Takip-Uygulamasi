import {
  ClipboardList,
  Plus,
  Users
} from "lucide-react";
import type { FormEvent } from "react";
import { useState } from "react";
import { demoPassword } from "../../app/auth/session";
import type { ApiClient } from "../../shared/api/client";
import { getErrorMessage } from "../../shared/errors/getErrorMessage";
import { PaginationControls } from "../../shared/pagination/PaginationControls";
import { usePagination } from "../../shared/pagination/usePagination";
import type { Notice } from "../../shared/types/ui";
import type {
  ManagedUser
} from "../../types";

export function UsersView({
  api,
  users,
  onChanged,
  setNotice
}: {
  api: ApiClient;
  users: ManagedUser[];
  onChanged: () => void;
  setNotice: (notice: Notice | null) => void;
}) {
  const [form, setForm] = useState({
    fullName: "",
    email: "",
    password: demoPassword,
    role: "Staff" as "Manager" | "Staff"
  });
  const userPagination = usePagination(users);

  async function submit(event: FormEvent) {
    event.preventDefault();
    setNotice(null);
    try {
      await api.createUser(form);
      setForm({ fullName: "", email: "", password: demoPassword, role: "Staff" });
      setNotice({ type: "success", message: "Kullanıcı oluşturuldu." });
      onChanged();
    } catch (error) {
      setNotice({ type: "error", message: getErrorMessage(error) });
    }
  }

  return (
    <div className="content-grid two-columns">
      <section className="tool-panel">
        <div className="section-title">
          <Users size={19} />
          <h2>Kullanıcı ekle</h2>
        </div>
        <form className="form-grid" onSubmit={submit}>
          <label>
            Ad soyad
            <input value={form.fullName} onChange={(event) => setForm({ ...form, fullName: event.target.value })} required />
          </label>
          <label>
            E-posta
            <input type="email" value={form.email} onChange={(event) => setForm({ ...form, email: event.target.value })} required />
          </label>
          <label>
            Şifre
            <input type="password" value={form.password} onChange={(event) => setForm({ ...form, password: event.target.value })} required />
          </label>
          <label>
            Rol
            <select value={form.role} onChange={(event) => setForm({ ...form, role: event.target.value as "Manager" | "Staff" })}>
              <option value="Staff">Personel</option>
              <option value="Manager">Yönetici</option>
            </select>
          </label>
          <button className="primary-action" type="submit">
            <Plus size={17} />
            Oluştur
          </button>
        </form>
      </section>

      <section className="tool-panel">
        <div className="section-title">
          <ClipboardList size={19} />
          <h2>Kullanıcılar</h2>
        </div>
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Ad</th>
                <th>E-posta</th>
                <th>Rol</th>
                <th>Durum</th>
              </tr>
            </thead>
            <tbody>
              {userPagination.items.map((managedUser) => (
                <tr key={managedUser.id}>
                  <td>{managedUser.fullName}</td>
                  <td>{managedUser.email}</td>
                  <td>{managedUser.role}</td>
                  <td>
                    <span className={managedUser.isActive ? "pill" : "pill warn"}>
                      {managedUser.isActive ? "Aktif" : "Pasif"}
                    </span>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <PaginationControls
          page={userPagination.page}
          totalPages={userPagination.totalPages}
          totalCount={userPagination.totalCount}
          startIndex={userPagination.startIndex}
          endIndex={userPagination.endIndex}
          onPageChange={userPagination.setPage}
        />
      </section>
    </div>
  );
}
