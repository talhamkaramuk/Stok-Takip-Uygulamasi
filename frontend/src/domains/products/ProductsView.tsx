import {
  Boxes,
  PackagePlus,
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
import { BarcodeScanner } from "../../shared/ui/BarcodeScanner";
import { appendBarcode } from "../../shared/utils/inventory";
import type {
  Category,
  Product
} from "../../types";

export function ProductsView({
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
  const [query, setQuery] = useState("");
  const [categoryId, setCategoryId] = useState("");
  const [form, setForm] = useState({
    sku: "",
    name: "",
    description: "",
    categoryName: "",
    criticalStockLevel: 1,
    initialStock: 0,
    barcodes: ""
  });
  const productPage = useServerPage<Product, { search?: string; categoryId?: string }>({
    filters: {
      search: query.trim() || undefined,
      categoryId: categoryId || undefined
    },
    load: api.listProducts,
    onError: (error) => setNotice({ type: "error", message: getErrorMessage(error) })
  });

  async function submit(event: FormEvent) {
    event.preventDefault();
    setNotice(null);
    try {
      await api.createProduct({
        ...form,
        description: form.description.trim() || null,
        categoryName: form.categoryName.trim() || null,
        barcodes: form.barcodes.split(/\r?\n|,/).map((value) => value.trim()).filter(Boolean)
      });
      setForm({ sku: "", name: "", description: "", categoryName: "", criticalStockLevel: 1, initialStock: 0, barcodes: "" });
      setNotice({ type: "success", message: "Ürün kaydedildi." });
      productPage.reload();
      onChanged();
    } catch (error) {
      setNotice({ type: "error", message: getErrorMessage(error) });
    }
  }

  function addScannedBarcode(value: string) {
    setForm((current) => ({
      ...current,
      barcodes: appendBarcode(current.barcodes, value)
    }));
    setNotice({ type: "success", message: "Barkod ürün formuna eklendi." });
  }

  return (
    <div className="content-grid two-columns">
      <section className="tool-panel">
        <div className="section-title">
          <PackagePlus size={19} />
          <h2>Ürün ekle</h2>
        </div>
        <form className="form-grid" onSubmit={submit}>
          <label>
            SKU
            <input value={form.sku} onChange={(event) => setForm({ ...form, sku: event.target.value })} required />
          </label>
          <label>
            Ürün adı
            <input value={form.name} onChange={(event) => setForm({ ...form, name: event.target.value })} required />
          </label>
          <label>
            Kategori
            <input list="category-options" value={form.categoryName} onChange={(event) => setForm({ ...form, categoryName: event.target.value })} />
            <datalist id="category-options">
              {categories.map((category) => (
                <option key={category.id} value={category.name} />
              ))}
            </datalist>
          </label>
          <div className="inline-fields">
            <label>
              Kritik
              <input type="number" min={0} value={form.criticalStockLevel} onChange={(event) => setForm({ ...form, criticalStockLevel: event.target.valueAsNumber })} />
            </label>
            <label>
              İlk stok
              <input type="number" min={0} value={form.initialStock} onChange={(event) => setForm({ ...form, initialStock: event.target.valueAsNumber })} />
            </label>
          </div>
          <label>
            Açıklama
            <textarea value={form.description} onChange={(event) => setForm({ ...form, description: event.target.value })} rows={3} />
          </label>
          <label>
            Barkodlar
            <textarea value={form.barcodes} onChange={(event) => setForm({ ...form, barcodes: event.target.value })} rows={3} />
          </label>
          <BarcodeScanner onDetect={addScannedBarcode} />
          <button className="primary-action" type="submit">
            <Plus size={17} />
            Kaydet
          </button>
        </form>
      </section>

      <section className="tool-panel">
        <div className="section-title spread">
          <span>
            <Boxes size={19} />
            <h2>Ürünler</h2>
          </span>
          <div className="table-filter-row">
            <label className="search-field">
              <Search size={16} />
              <input
                value={query}
                onChange={(event) => setQuery(event.target.value)}
                placeholder="SKU, ad veya barkod ara"
              />
            </label>
            <select value={categoryId} onChange={(event) => setCategoryId(event.target.value)}>
              <option value="">Tüm kategoriler</option>
              {categories.map((category) => (
                <option key={category.id} value={category.id}>{category.name}</option>
              ))}
            </select>
          </div>
        </div>
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>SKU</th>
                <th>Ürün</th>
                <th>Kategori</th>
                <th>Stok</th>
                <th>Barkod</th>
              </tr>
            </thead>
            <tbody>
              {productPage.items.map((product) => (
                <tr key={product.id}>
                  <td>{product.sku}</td>
                  <td>{product.name}</td>
                  <td>{product.categoryName || "-"}</td>
                  <td>
                    <span className={product.currentStock <= product.criticalStockLevel ? "pill warn" : "pill"}>
                      {product.currentStock}
                    </span>
                  </td>
                  <td>{product.barcodes[0] || "-"}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <PaginationControls
          page={productPage.page}
          totalPages={productPage.totalPages}
          totalCount={productPage.totalCount}
          startIndex={productPage.startIndex}
          endIndex={productPage.endIndex}
          onPageChange={productPage.setPage}
        />
      </section>
    </div>
  );
}
