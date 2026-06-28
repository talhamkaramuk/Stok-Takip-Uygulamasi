import {
  ClipboardList,
  Plus,
  Tags
} from "lucide-react";
import type { FormEvent } from "react";
import { useState } from "react";
import type { ApiClient } from "../../shared/api/client";
import { getErrorMessage } from "../../shared/errors/getErrorMessage";
import { PaginationControls } from "../../shared/pagination/PaginationControls";
import { usePagination } from "../../shared/pagination/usePagination";
import type { Notice } from "../../shared/types/ui";
import type {
  Category
} from "../../types";

export function CategoriesView({
  api,
  categories,
  onChanged,
  setNotice
}: {
  api: ApiClient;
  categories: Category[];
  onChanged: () => void;
  setNotice: (notice: Notice | null) => void;
}) {
  const [name, setName] = useState("");
  const categoryPagination = usePagination(categories);

  async function submit(event: FormEvent) {
    event.preventDefault();
    setNotice(null);
    try {
      await api.createCategory({ name });
      setName("");
      setNotice({ type: "success", message: "Kategori kaydedildi." });
      onChanged();
    } catch (error) {
      setNotice({ type: "error", message: getErrorMessage(error) });
    }
  }

  return (
    <div className="content-grid two-columns">
      <section className="tool-panel">
        <div className="section-title">
          <Tags size={19} />
          <h2>Kategori ekle</h2>
        </div>
        <form className="form-grid" onSubmit={submit}>
          <label>
            Kategori adı
            <input value={name} onChange={(event) => setName(event.target.value)} required />
          </label>
          <button className="primary-action" type="submit">
            <Plus size={17} />
            Kaydet
          </button>
        </form>
      </section>

      <section className="tool-panel">
        <div className="section-title">
          <ClipboardList size={19} />
          <h2>Kategoriler</h2>
        </div>
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Kategori</th>
                <th>Ürün</th>
                <th>Durum</th>
              </tr>
            </thead>
            <tbody>
              {categoryPagination.items.map((category) => (
                <tr key={category.id}>
                  <td>{category.name}</td>
                  <td>{category.productCount}</td>
                  <td>
                    <span className={category.isActive ? "pill" : "pill warn"}>
                      {category.isActive ? "Aktif" : "Pasif"}
                    </span>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <PaginationControls
          page={categoryPagination.page}
          totalPages={categoryPagination.totalPages}
          totalCount={categoryPagination.totalCount}
          startIndex={categoryPagination.startIndex}
          endIndex={categoryPagination.endIndex}
          onPageChange={categoryPagination.setPage}
        />
      </section>
    </div>
  );
}

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