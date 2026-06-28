import {
  AlertTriangle,
  Boxes,
  Check,
  FileSpreadsheet,
  Plus,
  RefreshCw,
  ShieldCheck
} from "lucide-react";
import type { FormEvent } from "react";
import { useState } from "react";
import type { ApiClient } from "../../shared/api/client";
import { getErrorMessage } from "../../shared/errors/getErrorMessage";
import type { Notice } from "../../shared/types/ui";
import { NoticeBox } from "../../shared/ui/NoticeBox";
import type { AuthResponse } from "../../types";
import { initialAuthForm } from "./session";

export function AuthScreen({
  api,
  onAuth,
  notice,
  setNotice
}: {
  api: ApiClient;
  onAuth: (response: AuthResponse) => void;
  notice: Notice | null;
  setNotice: (notice: Notice | null) => void;
}) {
  const [mode, setMode] = useState<"login" | "register">("login");
  const [loading, setLoading] = useState(false);
  const [form, setForm] = useState(initialAuthForm);

  async function submit(event: FormEvent) {
    event.preventDefault();
    setLoading(true);
    setNotice(null);

    try {
      const response =
        mode === "register"
          ? await api.registerTenant(form)
          : await api.login({ tenantSlug: form.tenantSlug, email: form.email, password: form.password });
      onAuth(response);
    } catch (error) {
      setNotice({ type: "error", message: getErrorMessage(error) });
    } finally {
      setLoading(false);
    }
  }

  return (
    <main className="auth-shell">
      <section className="auth-panel">
        <div className="brand-block">
          <div className="brand-mark">
            <Boxes size={28} />
          </div>
          <div>
            <h1>STOKIO</h1>
            <p>Barkod destekli stok takip ve sayım sistemi</p>
          </div>
        </div>

        <div className="mode-switch" role="tablist" aria-label="Oturum modu">
          <button className={mode === "login" ? "active" : ""} onClick={() => setMode("login")} type="button">
            <ShieldCheck size={16} />
            Giriş
          </button>
          <button className={mode === "register" ? "active" : ""} onClick={() => setMode("register")} type="button">
            <Plus size={16} />
            Kayıt
          </button>
        </div>

        {notice && <NoticeBox notice={notice} />}

        <form onSubmit={submit} className="form-grid">
          {mode === "register" && (
            <>
              <label>
                İşletme adı
                <input value={form.businessName} onChange={(event) => setForm({ ...form, businessName: event.target.value })} />
              </label>
              <label>
                Yetkili adı
                <input value={form.ownerName} onChange={(event) => setForm({ ...form, ownerName: event.target.value })} />
              </label>
            </>
          )}
          <label>
            Tenant
            <input value={form.tenantSlug} onChange={(event) => setForm({ ...form, tenantSlug: event.target.value })} />
          </label>
          <label>
            E-posta
            <input type="email" value={form.email} onChange={(event) => setForm({ ...form, email: event.target.value })} />
          </label>
          <label>
            Şifre
            <input type="password" value={form.password} onChange={(event) => setForm({ ...form, password: event.target.value })} />
          </label>
          <button className="primary-action" disabled={loading} type="submit">
            {loading ? <RefreshCw size={17} className="spin" /> : <Check size={17} />}
            {mode === "register" ? "İşletme oluştur" : "Giriş yap"}
          </button>
        </form>
      </section>

      <aside className="auth-insight" aria-label="STOKIO özeti">
        <div>
          <span className="eyebrow">STOKIO SaaS</span>
          <h2>Stok, sayım ve raporlar tek operasyon ekranında.</h2>
          <p>Telefon kamerasıyla barkod sayımı, kritik stok takibi ve Excel raporları için sade bir çalışma alanı.</p>
        </div>
        <div className="auth-preview">
          <div className="preview-row">
            <span className="preview-icon success"><Check size={18} /></span>
            <div>
              <strong>Sayım farkı</strong>
              <small>Beklenen ve sayılan stok aynı tabloda</small>
            </div>
          </div>
          <div className="preview-row">
            <span className="preview-icon warning"><AlertTriangle size={18} /></span>
            <div>
              <strong>Kritik stok</strong>
              <small>Eksilen ürünler anında görünür</small>
            </div>
          </div>
          <div className="preview-row">
            <span className="preview-icon info"><FileSpreadsheet size={18} /></span>
            <div>
              <strong>Excel çıktı</strong>
              <small>Raporlar tek tıkla dışa aktarılır</small>
            </div>
          </div>
        </div>
      </aside>
    </main>
  );
}