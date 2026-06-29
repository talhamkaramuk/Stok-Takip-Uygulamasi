import {
  ClipboardList,
  Plus,
  Search,
  Tags
} from "lucide-react";
import type { FormEvent } from "react";
import { useState } from "react";
import type { ApiClient } from "../../shared/api/client";
import { getErrorMessage } from "../../shared/errors/getErrorMessage";
import { PaginationControls } from "../../shared/pagination/PaginationControls";
import { useServerPage } from "../../shared/pagination/useServerPage";
import type { Notice } from "../../shared/types/ui";
import type { Category } from "../../types";

export function CategoriesView({
  api,
  onChanged,
  setNotice
}: {
  api: ApiClient;
  onChanged: () => void;
  setNotice: (notice: Notice | null) => void;
}) {
  const [name, setName] = useState("");
  const [query, setQuery] = useState("");
  const categoryPage = useServerPage<Category, { search?: string }>({
    filters: { search: query.trim() || undefined },
    load: api.listCategories,
    onError: (error) => setNotice({ type: "error", message: getErrorMessage(error) })
  });

  async function submit(event: FormEvent) {
    event.preventDefault();
    setNotice(null);
    try {
      await api.createCategory({ name });
      setName("");
      setNotice({ type: "success", message: "Kategori kaydedildi." });
      categoryPage.reload();
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
        <div className="section-title spread">
          <span>
            <ClipboardList size={19} />
            <h2>Kategoriler</h2>
          </span>
          <label className="search-field">
            <Search size={16} />
            <input value={query} onChange={(event) => setQuery(event.target.value)} placeholder="Kategori ara" />
          </label>
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
              {categoryPage.items.map((category) => (
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
          page={categoryPage.page}
          totalPages={categoryPage.totalPages}
          totalCount={categoryPage.totalCount}
          startIndex={categoryPage.startIndex}
          endIndex={categoryPage.endIndex}
          onPageChange={categoryPage.setPage}
        />
      </section>
    </div>
  );
}
