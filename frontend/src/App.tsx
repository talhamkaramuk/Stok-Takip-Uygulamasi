import {
  AlertTriangle,
  ArrowLeftRight,
  BarChart3,
  Boxes,
  Building2,
  Camera,
  ChevronDown,
  Check,
  ClipboardCheck,
  ClipboardList,
  Download,
  FileSpreadsheet,
  Handshake,
  LogOut,
  Menu,
  PackagePlus,
  PanelLeftClose,
  PanelLeftOpen,
  Plus,
  RotateCcw,
  RefreshCw,
  ScanLine,
  Search,
  ShieldCheck,
  Tags,
  Truck,
  UserCircle,
  Users,
  X
} from "lucide-react";
import type { IScannerControls } from "@zxing/browser";
import { useEffect, useMemo, useRef, useState } from "react";
import type { FormEvent, ReactNode } from "react";
import { createApiClient } from "./api";
import type {
  AuthResponse,
  Category,
  CountDifference,
  CriticalStock,
  Customer,
  InventoryCount,
  InventoryCountItem,
  ManagedUser,
  OperationItem,
  Product,
  PurchaseRequest,
  ReturnRequest,
  SalesOrder,
  Shipment,
  StockConsistency,
  StockMovement,
  StockMovementType,
  Supplier,
  Warehouse,
  WarehouseStock
} from "./types";

const legacyTokenStorageKey = "stokio.accessToken";
const legacyUserStorageKey = "stokio.user";
const demoCredentialsEnabled = import.meta.env.DEV || import.meta.env.VITE_ENABLE_DEMO_CREDENTIALS === "true";
const demoPassword = demoCredentialsEnabled ? "StrongPass123" : "";
const initialAuthForm = demoCredentialsEnabled
  ? {
      businessName: "STOKIO Demo",
      tenantSlug: "stokio-demo",
      ownerName: "Talha",
      email: "owner@stokio.local",
      password: demoPassword
    }
  : {
      businessName: "",
      tenantSlug: "",
      ownerName: "",
      email: "",
      password: ""
    };

type TabKey =
  | "dashboard"
  | "products"
  | "orders"
  | "purchase"
  | "shipments"
  | "returns"
  | "categories"
  | "customers"
  | "suppliers"
  | "warehouses"
  | "stock"
  | "count"
  | "users"
  | "profile"
  | "reports";

type Notice = {
  type: "success" | "error";
  message: string;
};

type MetricItem = {
  label: string;
  value: string;
  icon: ReactNode;
  tone?: "ok" | "warn";
};

type BarcodeReader = InstanceType<typeof import("@zxing/browser").BrowserMultiFormatReader>;

const tabMeta: Record<TabKey, { title: string; description: string }> = {
  dashboard: {
    title: "Ana Sayfa",
    description: "Operasyon hacmini, stok akışını ve kritik işleri canlı verilerle izleyin."
  },
  products: {
    title: "Ürünler",
    description: "Ürün kataloğunu, barkodları ve kritik stok seviyelerini yönetin."
  },
  categories: {
    title: "Kategoriler",
    description: "Ürün gruplarını düzenleyin ve katalog kırılımlarını takip edin."
  },
  customers: {
    title: "Müşteriler",
    description: "Satış, sevkiyat ve iade süreçlerinde kullanılan müşteri kartlarını yönetin."
  },
  suppliers: {
    title: "Tedarikçiler",
    description: "Alım taleplerinde kullanılan tedarikçi kartlarını ve iletişim bilgilerini yönetin."
  },
  orders: {
    title: "Siparişler",
    description: "Müşteri siparişlerini, hazırlanma durumunu ve sevkiyata çıkan ürünleri takip edin."
  },
  purchase: {
    title: "Alım Talep",
    description: "Tedarik taleplerini oluşturun, onaylayın ve teslim alındığında stoğa işleyin."
  },
  shipments: {
    title: "Sevkiyat",
    description: "Müşteri gönderilerini oluşturun ve sevkiyatla stok çıkışını kayıt altına alın."
  },
  returns: {
    title: "İadeler",
    description: "İade alınan ürünleri kaydedin ve uygun depoya stok girişi oluşturun."
  },
  warehouses: {
    title: "Depolar",
    description: "Şube, depo ve raf lokasyonlarına göre stok bakiyelerini ve transferleri yönetin."
  },
  stock: {
    title: "Stok Hareketleri",
    description: "Giriş, çıkış, düzeltme ve sayım kaynaklı stok hareketlerini kaydedin."
  },
  count: {
    title: "Sayım",
    description: "Barkodla sayım yapın, farkları görün ve stokla mutabakat sağlayın."
  },
  users: {
    title: "Kullanıcılar",
    description: "Ekip üyelerini ve rol bazlı erişimleri yönetin."
  },
  profile: {
    title: "Profilim",
    description: "Oturumdaki kullanıcı, rol ve çalışma alanı bilgilerini görüntüleyin."
  },
  reports: {
    title: "Raporlar",
    description: "Stok, kritik seviye, hareket ve sayım farkı raporlarını dışa aktarın."
  }
};

export default function App() {
  const [token, setToken] = useState<string | null>(null);
  const [user, setUser] = useState<AuthResponse["user"] | null>(null);
  const [notice, setNotice] = useState<Notice | null>(null);
  const api = useMemo(() => createApiClient(token), [token]);

  useEffect(() => {
    localStorage.removeItem(legacyTokenStorageKey);
    localStorage.removeItem(legacyUserStorageKey);
  }, []);

  function handleAuth(response: AuthResponse) {
    setNotice(null);
    setToken(response.accessToken);
    setUser(response.user);
  }

  function logout() {
    setNotice(null);
    setToken(null);
    setUser(null);
  }

  if (!token || !user) {
    return <AuthScreen api={api} onAuth={handleAuth} notice={notice} setNotice={setNotice} />;
  }

  return (
    <Workspace
      api={api}
      user={user}
      onLogout={logout}
      notice={notice}
      setNotice={setNotice}
    />
  );
}

function AuthScreen({
  api,
  onAuth,
  notice,
  setNotice
}: {
  api: ReturnType<typeof createApiClient>;
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

function Workspace({
  api,
  user,
  onLogout,
  notice,
  setNotice
}: {
  api: ReturnType<typeof createApiClient>;
  user: AuthResponse["user"];
  onLogout: () => void;
  notice: Notice | null;
  setNotice: (notice: Notice | null) => void;
}) {
  const sidebarRef = useRef<HTMLElement | null>(null);
  const userMenuRef = useRef<HTMLDivElement | null>(null);
  const [tab, setTab] = useState<TabKey>("dashboard");
  const [isSidebarCollapsed, setIsSidebarCollapsed] = useState(() => {
    if (typeof window === "undefined") {
      return false;
    }

    return window.matchMedia("(max-width: 1040px)").matches;
  });
  const [isSidebarDrawerOpen, setIsSidebarDrawerOpen] = useState(false);
  const [isUserMenuOpen, setIsUserMenuOpen] = useState(false);
  const [products, setProducts] = useState<Product[]>([]);
  const [categories, setCategories] = useState<Category[]>([]);
  const [customers, setCustomers] = useState<Customer[]>([]);
  const [suppliers, setSuppliers] = useState<Supplier[]>([]);
  const [warehouses, setWarehouses] = useState<Warehouse[]>([]);
  const [warehouseStock, setWarehouseStock] = useState<WarehouseStock[]>([]);
  const [users, setUsers] = useState<ManagedUser[]>([]);
  const [critical, setCritical] = useState<CriticalStock[]>([]);
  const [movements, setMovements] = useState<StockMovement[]>([]);
  const [orders, setOrders] = useState<SalesOrder[]>([]);
  const [purchaseRequests, setPurchaseRequests] = useState<PurchaseRequest[]>([]);
  const [shipments, setShipments] = useState<Shipment[]>([]);
  const [returns, setReturns] = useState<ReturnRequest[]>([]);
  const [consistency, setConsistency] = useState<StockConsistency[]>([]);
  const [activeCount, setActiveCount] = useState<InventoryCount | null>(null);
  const [lastScannedItem, setLastScannedItem] = useState<InventoryCountItem | null>(null);
  const [differences, setDifferences] = useState<CountDifference[]>([]);
  const [loading, setLoading] = useState(true);

  async function refresh() {
    setLoading(true);
    try {
      const [
        nextProducts,
        nextCritical,
        nextMovements,
        nextCategories,
        nextCustomers,
        nextSuppliers,
        nextWarehouses,
        nextWarehouseStock,
        nextOrders,
        nextPurchaseRequests,
        nextShipments,
        nextReturns,
        nextActiveCount
      ] = await Promise.all([
        api.listProducts(),
        api.listCriticalStock(),
        api.listStockMovements(),
        api.listCategories(),
        api.listCustomers(),
        api.listSuppliers(),
        api.listWarehouses(),
        api.listWarehouseStock(),
        api.listOrders(),
        api.listPurchaseRequests(),
        api.listShipments(),
        api.listReturns(),
        activeCount ? api.getCount(activeCount.id).catch(() => null) : Promise.resolve(null)
      ]);
      setProducts(nextProducts.items);
      setCritical(nextCritical);
      setMovements(nextMovements.items);
      setCategories(nextCategories.items);
      setCustomers(nextCustomers.items);
      setSuppliers(nextSuppliers.items);
      setWarehouses(nextWarehouses.items);
      setWarehouseStock(nextWarehouseStock);
      setOrders(nextOrders.items);
      setPurchaseRequests(nextPurchaseRequests.items);
      setShipments(nextShipments.items);
      setReturns(nextReturns.items);
      if (activeCount) {
        setActiveCount(nextActiveCount);
      }

      if (user.role === "Owner") {
        const nextUsers = await api.listUsers();
        setUsers(nextUsers.items);
      }

      if (user.role !== "Staff") {
        setConsistency(await api.checkStockConsistency());
      }
    } catch (error) {
      setNotice({ type: "error", message: getErrorMessage(error) });
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    void refresh();
  }, []);

  useEffect(() => {
    if (typeof window === "undefined") {
      return undefined;
    }

    const sidebarRailQuery = window.matchMedia("(max-width: 1040px)");
    const syncSidebarDensity = () => setIsSidebarCollapsed(sidebarRailQuery.matches);

    syncSidebarDensity();
    sidebarRailQuery.addEventListener("change", syncSidebarDensity);

    return () => {
      sidebarRailQuery.removeEventListener("change", syncSidebarDensity);
    };
  }, []);

  useEffect(() => {
    if (!isSidebarDrawerOpen) {
      return undefined;
    }

    const closeOnEscape = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        setIsSidebarDrawerOpen(false);
      }
    };

    document.addEventListener("keydown", closeOnEscape);

    return () => {
      document.removeEventListener("keydown", closeOnEscape);
    };
  }, [isSidebarDrawerOpen]);

  useEffect(() => {
    if (!isUserMenuOpen) {
      return undefined;
    }

    const closeOnOutsideClick = (event: MouseEvent | TouchEvent) => {
      const menu = userMenuRef.current;

      if (menu && !menu.contains(event.target as Node)) {
        setIsUserMenuOpen(false);
      }
    };

    const closeOnEscape = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        setIsUserMenuOpen(false);
      }
    };

    document.addEventListener("mousedown", closeOnOutsideClick);
    document.addEventListener("touchstart", closeOnOutsideClick);
    document.addEventListener("keydown", closeOnEscape);

    return () => {
      document.removeEventListener("mousedown", closeOnOutsideClick);
      document.removeEventListener("touchstart", closeOnOutsideClick);
      document.removeEventListener("keydown", closeOnEscape);
    };
  }, [isUserMenuOpen]);

  useEffect(() => {
    const sidebar = sidebarRef.current;

    if (!sidebar) {
      return undefined;
    }

    sidebar.addEventListener("wheel", containSidebarWheel, { passive: false });

    return () => {
      sidebar.removeEventListener("wheel", containSidebarWheel);
    };
  }, []);

  function changeTab(nextTab: TabKey) {
    setNotice(null);
    setTab(nextTab);
    setIsSidebarDrawerOpen(false);
    setIsUserMenuOpen(false);
  }

  function handleLogout() {
    setIsUserMenuOpen(false);
    onLogout();
  }

  async function loadDifferences(countId: string) {
    setDifferences(await api.listCountDifferences(countId));
  }

  const page = tabMeta[tab];
  const pageMetrics = buildPageMetrics(tab, {
    products,
    categories,
    customers,
    suppliers,
    warehouses,
    warehouseStock,
    critical,
    movements,
    orders,
    purchaseRequests,
    shipments,
    returns,
    activeCount,
    users
  });
  const initials = user.fullName
    .split(" ")
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase())
    .join("");

  const sidebarClasses = [
    "app-shell",
    isSidebarCollapsed ? "sidebar-collapsed" : "",
    isSidebarDrawerOpen ? "sidebar-drawer-open" : ""
  ]
    .filter(Boolean)
    .join(" ");

  return (
    <main className={sidebarClasses}>
      <button
        className="sidebar-backdrop"
        type="button"
        aria-label="Menüyü kapat"
        onClick={() => setIsSidebarDrawerOpen(false)}
      />

      <aside className="sidebar" id="main-sidebar" ref={sidebarRef}>
        <div className="brand-row">
          <div className="sidebar-brand">
            <div className="brand-mark compact">
              <Boxes size={20} />
            </div>
            <div className="brand-copy">
              <strong>STOKIO</strong>
              <span>Inventory OS</span>
            </div>
          </div>

          <div className="sidebar-controls">
            <button
              className="sidebar-icon-action sidebar-collapse-toggle"
              type="button"
              aria-label={isSidebarCollapsed ? "Menüyü genişlet" : "Menüyü daralt"}
              onClick={() => setIsSidebarCollapsed((value) => !value)}
            >
              {isSidebarCollapsed ? <PanelLeftOpen size={17} /> : <PanelLeftClose size={17} />}
            </button>
            <button
              className="sidebar-icon-action sidebar-close"
              type="button"
              aria-label="Menüyü kapat"
              onClick={() => setIsSidebarDrawerOpen(false)}
            >
              <X size={18} />
            </button>
          </div>
        </div>

        <nav className="nav-tabs" aria-label="Ana modüller">
          <TabButton active={tab === "dashboard"} onClick={() => changeTab("dashboard")} icon={<BarChart3 size={18} />} label="Ana Sayfa" />
          <span className="nav-section-label">Operasyon</span>
          <TabButton active={tab === "products"} onClick={() => changeTab("products")} icon={<PackagePlus size={18} />} label="Envanter" />
          <TabButton active={tab === "orders"} onClick={() => changeTab("orders")} icon={<ClipboardCheck size={18} />} label="Siparişler" />
          <TabButton active={tab === "purchase"} onClick={() => changeTab("purchase")} icon={<Download size={18} />} label="Alım Talep" />
          <TabButton active={tab === "shipments"} onClick={() => changeTab("shipments")} icon={<Truck size={18} />} label="Sevkiyat" />
          <TabButton active={tab === "returns"} onClick={() => changeTab("returns")} icon={<RotateCcw size={18} />} label="İadeler" />
          <TabButton active={tab === "stock"} onClick={() => changeTab("stock")} icon={<Boxes size={18} />} label="Stok Hareketleri" />
          <TabButton active={tab === "count"} onClick={() => changeTab("count")} icon={<ScanLine size={18} />} label="Sayım İşlemleri" />
          <TabButton active={tab === "warehouses"} onClick={() => changeTab("warehouses")} icon={<Boxes size={18} />} label="Depolar" />
          <span className="nav-section-label">Yönetim</span>
          <TabButton active={tab === "customers"} onClick={() => changeTab("customers")} icon={<Users size={18} />} label="Müşteriler" />
          <TabButton active={tab === "suppliers"} onClick={() => changeTab("suppliers")} icon={<Handshake size={18} />} label="Tedarikçiler" />
          <TabButton active={tab === "categories"} onClick={() => changeTab("categories")} icon={<Tags size={18} />} label="Kategoriler" />
          {user.role === "Owner" && (
            <TabButton active={tab === "users"} onClick={() => changeTab("users")} icon={<Users size={18} />} label="Kullanıcılar" />
          )}
          <TabButton active={tab === "reports"} onClick={() => changeTab("reports")} icon={<BarChart3 size={18} />} label="Raporlar" />
        </nav>

      </aside>

      <section className="workspace">
        <header className="topbar">
          <div className="topbar-title">
            <button
              className="sidebar-menu-button"
              type="button"
              aria-label="Menüyü aç"
              aria-controls="main-sidebar"
              aria-expanded={isSidebarDrawerOpen}
              onClick={() => setIsSidebarDrawerOpen(true)}
            >
              <Menu size={19} />
            </button>
            <div className="tenant-context">
              <span className="eyebrow">Çalışma alanı</span>
              <strong>{user.tenantSlug}</strong>
            </div>
          </div>
          <div className="topbar-actions">
            <div className="user-menu" ref={userMenuRef}>
              <button
                className="user-chip"
                type="button"
                aria-haspopup="menu"
                aria-expanded={isUserMenuOpen}
                onClick={() => setIsUserMenuOpen((value) => !value)}
              >
                <span className="user-avatar">{initials || "ST"}</span>
                <div className="user-chip-copy">
                  <strong>{user.fullName}</strong>
                  <small>{user.role}</small>
                </div>
                <ChevronDown size={15} className={isUserMenuOpen ? "chevron-open" : ""} />
              </button>

              {isUserMenuOpen && (
                <div className="user-dropdown" role="menu">
                  <div className="user-dropdown-header">
                    <span className="user-avatar large">{initials || "ST"}</span>
                    <div>
                      <strong>{user.fullName}</strong>
                      <small>{user.email}</small>
                    </div>
                  </div>

                  <div className="user-dropdown-meta">
                    <span>
                      <ShieldCheck size={15} />
                      {user.role}
                    </span>
                    <span>
                      <Building2 size={15} />
                      {user.tenantSlug}
                    </span>
                  </div>

                  <div className="user-dropdown-section">
                    <button type="button" role="menuitem" onClick={() => changeTab("profile")}>
                      <UserCircle size={17} />
                      <span>
                        <strong>Profilim</strong>
                        <small>Hesap ve oturum bilgileri</small>
                      </span>
                    </button>
                    {user.role === "Owner" && (
                      <button type="button" role="menuitem" onClick={() => changeTab("users")}>
                        <Users size={17} />
                        <span>
                          <strong>Kullanıcı yönetimi</strong>
                          <small>Ekip ve rol erişimleri</small>
                        </span>
                      </button>
                    )}
                  </div>

                  <button className="user-dropdown-logout" type="button" role="menuitem" onClick={handleLogout}>
                    <LogOut size={17} />
                    Çıkış yap
                  </button>
                </div>
              )}
            </div>
            <button className="ghost-action" onClick={() => void refresh()} type="button">
              <RefreshCw size={17} className={loading ? "spin" : ""} />
              Yenile
            </button>
          </div>
        </header>

        <section className="page-heading">
          <nav className="breadcrumbs" aria-label="Konum">
            <span>Stok</span>
            <span>/</span>
            <strong>{page.title}</strong>
          </nav>
          <div>
            <h1>{page.title}</h1>
            <p>{page.description}</p>
          </div>
        </section>

        {notice && <NoticeBox notice={notice} />}

        {pageMetrics.length > 0 && (
          <section className="metric-grid">
            {pageMetrics.map((metric) => (
              <Metric key={metric.label} {...metric} />
            ))}
          </section>
        )}

        {tab === "dashboard" && (
          <DashboardView
            products={products}
            critical={critical}
            movements={movements}
            orders={orders}
            purchaseRequests={purchaseRequests}
            shipments={shipments}
            returns={returns}
            warehouses={warehouses}
            warehouseStock={warehouseStock}
            customers={customers}
            suppliers={suppliers}
          />
        )}
        {tab === "products" && (
          <ProductsView
            api={api}
            products={products}
            categories={categories}
            onChanged={() => void refresh()}
            setNotice={setNotice}
          />
        )}
        {tab === "categories" && (
          <CategoriesView
            api={api}
            categories={categories}
            onChanged={() => void refresh()}
            setNotice={setNotice}
          />
        )}
        {tab === "customers" && (
          <CustomersView
            api={api}
            customers={customers}
            onChanged={() => void refresh()}
            setNotice={setNotice}
          />
        )}
        {tab === "suppliers" && (
          <SuppliersView
            api={api}
            suppliers={suppliers}
            onChanged={() => void refresh()}
            setNotice={setNotice}
          />
        )}
        {tab === "orders" && (
          <OrdersView
            api={api}
            orders={orders}
            products={products}
            warehouses={warehouses}
            customers={customers}
            onChanged={() => void refresh()}
            setNotice={setNotice}
          />
        )}
        {tab === "purchase" && (
          <PurchaseRequestsView
            api={api}
            purchaseRequests={purchaseRequests}
            products={products}
            warehouses={warehouses}
            suppliers={suppliers}
            onChanged={() => void refresh()}
            setNotice={setNotice}
          />
        )}
        {tab === "shipments" && (
          <ShipmentsView
            api={api}
            shipments={shipments}
            orders={orders}
            products={products}
            warehouses={warehouses}
            customers={customers}
            onChanged={() => void refresh()}
            setNotice={setNotice}
          />
        )}
        {tab === "returns" && (
          <ReturnsView
            api={api}
            returns={returns}
            orders={orders}
            products={products}
            warehouses={warehouses}
            customers={customers}
            onChanged={() => void refresh()}
            setNotice={setNotice}
          />
        )}
        {tab === "warehouses" && (
          <WarehousesView
            api={api}
            warehouses={warehouses}
            warehouseStock={warehouseStock}
            products={products}
            onChanged={() => void refresh()}
            setNotice={setNotice}
          />
        )}
        {tab === "stock" && (
          <StockView
            api={api}
            products={products}
            warehouses={warehouses}
            movements={movements}
            onChanged={() => void refresh()}
            setNotice={setNotice}
          />
        )}
        {tab === "count" && (
          <CountView
            api={api}
            products={products}
            warehouses={warehouses}
            activeCount={activeCount}
            setActiveCount={setActiveCount}
            lastScannedItem={lastScannedItem}
            setLastScannedItem={setLastScannedItem}
            differences={differences}
            loadDifferences={loadDifferences}
            onChanged={() => void refresh()}
            setNotice={setNotice}
          />
        )}
        {tab === "users" && user.role === "Owner" && (
          <UsersView
            api={api}
            users={users}
            onChanged={() => void refresh()}
            setNotice={setNotice}
          />
        )}
        {tab === "profile" && <ProfileView user={user} />}
        {tab === "reports" && (
          <ReportsView
            api={api}
            critical={critical}
            movements={movements}
            consistency={consistency}
            activeCount={activeCount}
            setNotice={setNotice}
          />
        )}
      </section>
    </main>
  );
}

function ProfileView({ user }: { user: AuthResponse["user"] }) {
  return (
    <div className="content-grid">
      <section className="tool-panel profile-panel">
        <div className="section-title">
          <UserCircle size={19} />
          <h2>Hesap bilgileri</h2>
        </div>

        <div className="profile-summary">
          <span className="user-avatar xlarge">
            {user.fullName
              .split(" ")
              .filter(Boolean)
              .slice(0, 2)
              .map((part) => part[0]?.toUpperCase())
              .join("") || "ST"}
          </span>
          <div>
            <h2>{user.fullName}</h2>
            <p>{user.email}</p>
          </div>
        </div>

        <div className="profile-grid">
          <div className="profile-field">
            <span>Kullanıcı ID</span>
            <strong>{user.id}</strong>
          </div>
          <div className="profile-field">
            <span>Rol</span>
            <strong>{user.role}</strong>
          </div>
          <div className="profile-field">
            <span>Tenant</span>
            <strong>{user.tenantSlug}</strong>
          </div>
          <div className="profile-field">
            <span>Tenant ID</span>
            <strong>{user.tenantId}</strong>
          </div>
        </div>
      </section>
    </div>
  );
}

function buildPageMetrics(
  tab: TabKey,
  data: {
    products: Product[];
    categories: Category[];
    customers: Customer[];
    suppliers: Supplier[];
    warehouses: Warehouse[];
    warehouseStock: WarehouseStock[];
    critical: CriticalStock[];
    movements: StockMovement[];
    orders: SalesOrder[];
    purchaseRequests: PurchaseRequest[];
    shipments: Shipment[];
    returns: ReturnRequest[];
    activeCount: InventoryCount | null;
    users: ManagedUser[];
  }
): MetricItem[] {
  const activeProducts = data.products.filter((product) => product.isActive).length;
  const totalStock = data.products.reduce((sum, product) => sum + product.currentStock, 0);
  const stockIn = data.movements.filter((movement) => movement.type === "In" || movement.type === "TransferIn").length;
  const stockOut = data.movements.filter((movement) => movement.type === "Out" || movement.type === "TransferOut").length;
  const barcodedProducts = data.products.filter((product) => product.barcodes.length > 0).length;
  const activeWarehouses = data.warehouses.filter((warehouse) => warehouse.isActive).length;

  switch (tab) {
    case "dashboard":
      return [
        { label: "Aktif ürün", value: activeProducts.toString(), icon: <PackagePlus size={19} /> },
        { label: "Toplam stok", value: totalStock.toString(), icon: <Boxes size={19} /> },
        { label: "Sipariş", value: data.orders.length.toString(), icon: <ClipboardCheck size={19} /> },
        { label: "Sevkiyat", value: data.shipments.length.toString(), icon: <Truck size={19} /> },
        { label: "Kritik stok", value: data.critical.length.toString(), icon: <AlertTriangle size={19} />, tone: data.critical.length > 0 ? "warn" : "ok" }
      ];
    case "products":
      return [
        { label: "Aktif ürün", value: activeProducts.toString(), icon: <PackagePlus size={19} /> },
        { label: "Barkodlu ürün", value: barcodedProducts.toString(), icon: <ScanLine size={19} /> },
        { label: "Kategori", value: data.categories.length.toString(), icon: <Tags size={19} /> },
        { label: "Toplam stok", value: totalStock.toString(), icon: <Boxes size={19} /> },
        { label: "Kritik stok", value: data.critical.length.toString(), icon: <AlertTriangle size={19} />, tone: data.critical.length > 0 ? "warn" : "ok" }
      ];
    case "customers":
      return [
        { label: "Toplam müşteri", value: data.customers.length.toString(), icon: <Users size={19} /> },
        { label: "Aktif müşteri", value: data.customers.filter((customer) => customer.isActive).length.toString(), icon: <Check size={19} />, tone: "ok" },
        { label: "Pasif müşteri", value: data.customers.filter((customer) => !customer.isActive).length.toString(), icon: <AlertTriangle size={19} /> }
      ];
    case "suppliers":
      return [
        { label: "Toplam tedarikçi", value: data.suppliers.length.toString(), icon: <Handshake size={19} /> },
        { label: "Aktif tedarikçi", value: data.suppliers.filter((supplier) => supplier.isActive).length.toString(), icon: <Check size={19} />, tone: "ok" },
        { label: "Pasif tedarikçi", value: data.suppliers.filter((supplier) => !supplier.isActive).length.toString(), icon: <AlertTriangle size={19} /> }
      ];
    case "orders":
      return [
        { label: "Toplam sipariş", value: data.orders.length.toString(), icon: <ClipboardCheck size={19} /> },
        { label: "Bekleyen", value: data.orders.filter((order) => order.status === "Pending").length.toString(), icon: <ClipboardList size={19} /> },
        { label: "Kısmi sevk", value: data.orders.filter((order) => order.status === "PartiallyShipped").length.toString(), icon: <Truck size={19} /> },
        { label: "Sevk edildi", value: data.orders.filter((order) => order.status === "Shipped").length.toString(), icon: <Truck size={19} /> },
        { label: "İptal", value: data.orders.filter((order) => order.status === "Cancelled").length.toString(), icon: <AlertTriangle size={19} />, tone: "warn" }
      ];
    case "purchase":
      return [
        { label: "Toplam talep", value: data.purchaseRequests.length.toString(), icon: <Download size={19} /> },
        { label: "Onay bekliyor", value: data.purchaseRequests.filter((request) => request.status === "PendingApproval").length.toString(), icon: <ClipboardList size={19} /> },
        { label: "Onaylandı", value: data.purchaseRequests.filter((request) => request.status === "Approved").length.toString(), icon: <Check size={19} />, tone: "ok" },
        { label: "Kısmi teslim", value: data.purchaseRequests.filter((request) => request.status === "PartiallyReceived").length.toString(), icon: <Boxes size={19} /> },
        { label: "Teslim alındı", value: data.purchaseRequests.filter((request) => request.status === "Received").length.toString(), icon: <Boxes size={19} /> },
      ];
    case "shipments":
      return [
        { label: "Toplam sevkiyat", value: data.shipments.length.toString(), icon: <Truck size={19} /> },
        { label: "Tamamlandı", value: data.shipments.filter((shipment) => shipment.status === "Completed").length.toString(), icon: <Check size={19} />, tone: "ok" },
        { label: "İptal", value: data.shipments.filter((shipment) => shipment.status === "Cancelled").length.toString(), icon: <AlertTriangle size={19} />, tone: "warn" },
        { label: "Sevk edilen adet", value: data.shipments.reduce((sum, shipment) => sum + shipment.totalQuantity, 0).toString(), icon: <Boxes size={19} /> }
      ];
    case "returns":
      return [
        { label: "Toplam iade", value: data.returns.length.toString(), icon: <RotateCcw size={19} /> },
        { label: "Teslim alındı", value: data.returns.filter((item) => item.status === "Received").length.toString(), icon: <Check size={19} />, tone: "ok" },
        { label: "Reddedildi", value: data.returns.filter((item) => item.status === "Rejected").length.toString(), icon: <AlertTriangle size={19} />, tone: "warn" },
        { label: "İade adedi", value: data.returns.reduce((sum, item) => sum + item.totalQuantity, 0).toString(), icon: <Boxes size={19} /> }
      ];
    case "warehouses":
      return [
        { label: "Aktif depo", value: activeWarehouses.toString(), icon: <Boxes size={19} /> },
        { label: "Varsayılan depo", value: data.warehouses.filter((warehouse) => warehouse.isDefault).length.toString(), icon: <Check size={19} />, tone: "ok" },
        { label: "Depo stoku", value: data.warehouseStock.reduce((sum, item) => sum + item.quantity, 0).toString(), icon: <PackagePlus size={19} /> },
        { label: "Kritik depo satırı", value: data.warehouseStock.filter((item) => item.isCritical).length.toString(), icon: <AlertTriangle size={19} />, tone: data.warehouseStock.some((item) => item.isCritical) ? "warn" : "ok" }
      ];
    case "stock":
      return [
        { label: "Hareket", value: data.movements.length.toString(), icon: <ClipboardList size={19} /> },
        { label: "Giriş", value: stockIn.toString(), icon: <Download size={19} />, tone: "ok" },
        { label: "Çıkış", value: stockOut.toString(), icon: <Truck size={19} /> },
        { label: "Sayım düzeltme", value: data.movements.filter((movement) => movement.type === "CountCorrection").length.toString(), icon: <ScanLine size={19} /> }
      ];
    case "count":
      return [
        { label: "Sayım durumu", value: data.activeCount?.status === "Open" ? "Açık" : "Kapalı", icon: <ScanLine size={19} />, tone: data.activeCount?.status === "Open" ? "ok" : undefined },
        { label: "Sayılan ürün", value: (data.activeCount?.itemCount ?? 0).toString(), icon: <Boxes size={19} /> },
        { label: "Fark", value: (data.activeCount?.differenceCount ?? 0).toString(), icon: <AlertTriangle size={19} />, tone: (data.activeCount?.differenceCount ?? 0) > 0 ? "warn" : "ok" },
        { label: "Barkodlu ürün", value: barcodedProducts.toString(), icon: <ScanLine size={19} /> }
      ];
    case "users":
      return [
        { label: "Toplam kullanıcı", value: data.users.length.toString(), icon: <Users size={19} /> },
        { label: "Aktif kullanıcı", value: data.users.filter((user) => user.isActive).length.toString(), icon: <Check size={19} />, tone: "ok" },
        { label: "Yönetici", value: data.users.filter((user) => user.role === "Owner" || user.role === "Manager").length.toString(), icon: <ShieldCheck size={19} /> }
      ];
    default:
      return [];
  }
}

function usePagination<T>(items: T[], pageSize = 8) {
  const [page, setPage] = useState(1);
  const totalCount = items.length;
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));
  const currentPage = Math.min(page, totalPages);
  const startIndex = totalCount === 0 ? 0 : (currentPage - 1) * pageSize;
  const endIndex = Math.min(startIndex + pageSize, totalCount);

  useEffect(() => {
    setPage((value) => Math.min(Math.max(value, 1), totalPages));
  }, [totalPages]);

  return {
    items: items.slice(startIndex, endIndex),
    page: currentPage,
    pageSize,
    totalCount,
    totalPages,
    startIndex,
    endIndex,
    setPage
  };
}

function PaginationControls({
  page,
  totalPages,
  totalCount,
  startIndex,
  endIndex,
  onPageChange
}: {
  page: number;
  totalPages: number;
  totalCount: number;
  startIndex: number;
  endIndex: number;
  onPageChange: (page: number) => void;
}) {
  return (
    <div className="pagination-bar">
      <span className="pagination-info">
        {totalCount === 0 ? "Kayıt yok" : `${startIndex + 1}-${endIndex} / ${totalCount} kayıt`}
      </span>
      <div className="pagination-buttons" aria-label="Sayfalama">
        <button className="ghost-action compact-action" type="button" disabled={page <= 1} onClick={() => onPageChange(page - 1)}>
          Önceki
        </button>
        <span>{page} / {totalPages}</span>
        <button className="ghost-action compact-action" type="button" disabled={page >= totalPages} onClick={() => onPageChange(page + 1)}>
          Sonraki
        </button>
      </div>
    </div>
  );
}

function DashboardView({
  products,
  critical,
  movements,
  orders,
  purchaseRequests,
  shipments,
  returns,
  warehouses,
  warehouseStock,
  customers,
  suppliers
}: {
  products: Product[];
  critical: CriticalStock[];
  movements: StockMovement[];
  orders: SalesOrder[];
  purchaseRequests: PurchaseRequest[];
  shipments: Shipment[];
  returns: ReturnRequest[];
  warehouses: Warehouse[];
  warehouseStock: WarehouseStock[];
  customers: Customer[];
  suppliers: Supplier[];
}) {
  const operationTrend = buildOperationTrend(orders, purchaseRequests, shipments, returns);
  const stockFlow = buildStockFlow(movements);
  const operationBars = [
    { label: "Sipariş", value: orders.length, tone: "primary" },
    { label: "Alım", value: purchaseRequests.length, tone: "success" },
    { label: "Sevkiyat", value: shipments.length, tone: "info" },
    { label: "İade", value: returns.length, tone: "warning" }
  ];
  const pendingJobs = [
    { label: "Bekleyen sipariş", value: orders.filter((order) => order.status === "Pending" || order.status === "PartiallyShipped").length },
    { label: "Onay bekleyen alım", value: purchaseRequests.filter((request) => request.status === "PendingApproval").length },
    { label: "Teslim alınacak alım", value: purchaseRequests.filter((request) => request.status === "Approved" || request.status === "PartiallyReceived").length },
    { label: "Kritik stok", value: critical.length }
  ];
  const warehouseBars = buildWarehouseBars(warehouses, warehouseStock);
  const topProducts = buildTopOperationProducts(orders, purchaseRequests, shipments, returns);
  const recentOperations = buildRecentOperations(orders, purchaseRequests, shipments, returns);
  const recentOperationPagination = usePagination(recentOperations);
  const activeCustomers = customers.filter((customer) => customer.isActive).length;
  const activeSuppliers = suppliers.filter((supplier) => supplier.isActive).length;
  const stockIn = stockFlow.reduce((sum, point) => sum + point.inbound, 0);
  const stockOut = stockFlow.reduce((sum, point) => sum + point.outbound, 0);

  return (
    <div className="dashboard-grid">
      <section className="tool-panel dashboard-span-2">
        <div className="section-title spread">
          <span>
            <BarChart3 size={19} />
            <h2>Operasyon trendi</h2>
          </span>
          <small>Son 14 gün</small>
        </div>
        <OperationTrendChart points={operationTrend} />
      </section>

      <section className="tool-panel">
        <div className="section-title">
          <ClipboardList size={19} />
          <h2>Operasyon dağılımı</h2>
        </div>
        <HorizontalBars rows={operationBars} />
      </section>

      <section className="tool-panel dashboard-span-2">
        <div className="section-title spread">
          <span>
            <ArrowLeftRight size={19} />
            <h2>Stok akışı</h2>
          </span>
          <small>Giriş {stockIn} · Çıkış {stockOut}</small>
        </div>
        <StockFlowChart points={stockFlow} />
      </section>

      <section className="tool-panel">
        <div className="section-title">
          <AlertTriangle size={19} />
          <h2>Bekleyen işler</h2>
        </div>
        <div className="insight-list">
          {pendingJobs.map((item) => (
            <article className="insight-row" key={item.label}>
              <span>{item.label}</span>
              <strong>{item.value}</strong>
            </article>
          ))}
        </div>
      </section>

      <section className="tool-panel">
        <div className="section-title">
          <Boxes size={19} />
          <h2>Depo doluluğu</h2>
        </div>
        <HorizontalBars rows={warehouseBars} />
      </section>

      <section className="tool-panel">
        <div className="section-title">
          <PackagePlus size={19} />
          <h2>Operasyondaki ürünler</h2>
        </div>
        <div className="rank-list">
          {topProducts.length === 0 ? (
            <p className="empty-note">Operasyon kalemi bulunmuyor.</p>
          ) : (
            topProducts.map((item, index) => (
              <article className="rank-row" key={item.productId}>
                <span>{index + 1}</span>
                <div>
                  <strong>{item.sku}</strong>
                  <small>{item.productName}</small>
                </div>
                <b>{item.quantity}</b>
              </article>
            ))
          )}
        </div>
      </section>

      <section className="tool-panel">
        <div className="section-title">
          <Users size={19} />
          <h2>Cari kapsama</h2>
        </div>
        <div className="coverage-grid">
          <div>
            <strong>{activeCustomers}</strong>
            <span>Aktif müşteri</span>
          </div>
          <div>
            <strong>{activeSuppliers}</strong>
            <span>Aktif tedarikçi</span>
          </div>
          <div>
            <strong>{products.filter((product) => product.isActive).length}</strong>
            <span>Aktif ürün</span>
          </div>
        </div>
      </section>

      <section className="tool-panel dashboard-span-3">
        <div className="section-title">
          <ClipboardCheck size={19} />
          <h2>Son operasyonlar</h2>
        </div>
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Tip</th>
                <th>No</th>
                <th>Taraf</th>
                <th>Adet</th>
                <th>Durum</th>
                <th>Tarih</th>
              </tr>
            </thead>
            <tbody>
              {recentOperationPagination.items.map((item) => (
                <tr key={`${item.type}-${item.id}`}>
                  <td>{item.type}</td>
                  <td>{item.number}</td>
                  <td>{item.party}</td>
                  <td>{item.quantity}</td>
                  <td><span className={statusClass(item.status)}>{statusLabel(item.status)}</span></td>
                  <td>{formatDate(item.date)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <PaginationControls
          page={recentOperationPagination.page}
          totalPages={recentOperationPagination.totalPages}
          totalCount={recentOperationPagination.totalCount}
          startIndex={recentOperationPagination.startIndex}
          endIndex={recentOperationPagination.endIndex}
          onPageChange={recentOperationPagination.setPage}
        />
      </section>
    </div>
  );
}

function OperationTrendChart({ points }: { points: Array<{ label: string; total: number }> }) {
  const max = Math.max(1, ...points.map((point) => point.total));
  const width = 640;
  const height = 220;
  const padding = 26;
  const step = points.length > 1 ? (width - padding * 2) / (points.length - 1) : width - padding * 2;
  const coordinates = points.map((point, index) => {
    const x = padding + index * step;
    const y = height - padding - (point.total / max) * (height - padding * 2);
    return { ...point, x, y };
  });
  const line = coordinates.map((point) => `${point.x},${point.y}`).join(" ");
  const area = `${padding},${height - padding} ${line} ${width - padding},${height - padding}`;

  return (
    <div className="chart-shell">
      <svg viewBox={`0 0 ${width} ${height}`} role="img" aria-label="Operasyon trendi">
        <polygon className="chart-area" points={area} />
        <polyline className="chart-line" points={line} />
        {coordinates.map((point) => (
          <g key={point.label}>
            <circle className="chart-point" cx={point.x} cy={point.y} r="4" />
            <text x={point.x} y={height - 6} textAnchor="middle">{point.label}</text>
          </g>
        ))}
      </svg>
    </div>
  );
}

function StockFlowChart({ points }: { points: Array<{ label: string; inbound: number; outbound: number }> }) {
  const max = Math.max(1, ...points.flatMap((point) => [point.inbound, point.outbound]));

  return (
    <div className="flow-chart" aria-label="Stok giriş çıkış grafiği">
      {points.map((point) => (
        <div className="flow-day" key={point.label}>
          <div className="flow-bars">
            <span className="flow-in" style={{ height: `${Math.max(4, (point.inbound / max) * 100)}%` }} title={`Giriş ${point.inbound}`} />
            <span className="flow-out" style={{ height: `${Math.max(4, (point.outbound / max) * 100)}%` }} title={`Çıkış ${point.outbound}`} />
          </div>
          <small>{point.label}</small>
        </div>
      ))}
    </div>
  );
}

function HorizontalBars({ rows }: { rows: Array<{ label: string; value: number; tone?: string }> }) {
  const max = Math.max(1, ...rows.map((row) => row.value));

  return (
    <div className="bar-list">
      {rows.map((row) => (
        <article className="bar-row" key={row.label}>
          <div>
            <span>{row.label}</span>
            <strong>{row.value}</strong>
          </div>
          <b className={row.tone ? `bar-fill ${row.tone}` : "bar-fill"} style={{ width: `${Math.max(6, (row.value / max) * 100)}%` }} />
        </article>
      ))}
    </div>
  );
}

function ProductsView({
  api,
  products,
  categories,
  onChanged,
  setNotice
}: {
  api: ReturnType<typeof createApiClient>;
  products: Product[];
  categories: Category[];
  onChanged: () => void;
  setNotice: (notice: Notice | null) => void;
}) {
  const [query, setQuery] = useState("");
  const [form, setForm] = useState({
    sku: "",
    name: "",
    description: "",
    categoryName: "",
    criticalStockLevel: 1,
    initialStock: 0,
    barcodes: ""
  });

  const filteredProducts = products.filter((product) => {
    const term = query.trim().toLowerCase();
    if (!term) {
      return true;
    }

    return [product.sku, product.name, product.categoryName ?? "", ...product.barcodes]
      .some((value) => value.toLowerCase().includes(term));
  });
  const productPagination = usePagination(filteredProducts);

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
          <label className="search-field">
            <Search size={16} />
            <input value={query} onChange={(event) => setQuery(event.target.value)} />
          </label>
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
              {productPagination.items.map((product) => (
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
          page={productPagination.page}
          totalPages={productPagination.totalPages}
          totalCount={productPagination.totalCount}
          startIndex={productPagination.startIndex}
          endIndex={productPagination.endIndex}
          onPageChange={productPagination.setPage}
        />
      </section>
    </div>
  );
}

function CategoriesView({
  api,
  categories,
  onChanged,
  setNotice
}: {
  api: ReturnType<typeof createApiClient>;
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

function CustomersView({
  api,
  customers,
  onChanged,
  setNotice
}: {
  api: ReturnType<typeof createApiClient>;
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

function SuppliersView({
  api,
  suppliers,
  onChanged,
  setNotice
}: {
  api: ReturnType<typeof createApiClient>;
  suppliers: Supplier[];
  onChanged: () => void;
  setNotice: (notice: Notice | null) => void;
}) {
  const [editingId, setEditingId] = useState<string | null>(null);
  const [form, setForm] = useState(emptySupplierForm);
  const supplierPagination = usePagination(suppliers);

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
        <div className="section-title">
          <ClipboardList size={19} />
          <h2>Tedarikçiler</h2>
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
              {supplierPagination.items.map((supplier) => (
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
          page={supplierPagination.page}
          totalPages={supplierPagination.totalPages}
          totalCount={supplierPagination.totalCount}
          startIndex={supplierPagination.startIndex}
          endIndex={supplierPagination.endIndex}
          onPageChange={supplierPagination.setPage}
        />
      </section>
    </div>
  );
}

function OrdersView({
  api,
  products,
  warehouses,
  customers,
  orders,
  onChanged,
  setNotice
}: {
  api: ReturnType<typeof createApiClient>;
  products: Product[];
  warehouses: Warehouse[];
  customers: Customer[];
  orders: SalesOrder[];
  onChanged: () => void;
  setNotice: (notice: Notice | null) => void;
}) {
  const [form, setForm] = useState({ customerId: "", customerName: "", productId: "", warehouseId: "", quantity: 1, notes: "" });
  const activeWarehouses = warehouses.filter((warehouse) => warehouse.isActive);
  const activeCustomers = customers.filter((customer) => customer.isActive);
  const selectedWarehouseId = form.warehouseId || getDefaultWarehouseId(activeWarehouses);

  function selectCustomer(customerId: string) {
    const customer = activeCustomers.find((item) => item.id === customerId);
    setForm({ ...form, customerId, customerName: customer?.name ?? form.customerName });
  }

  async function submit(event: FormEvent) {
    event.preventDefault();
    setNotice(null);
    try {
      await api.createOrder({
        customerId: form.customerId || null,
        customerName: form.customerName,
        warehouseId: selectedWarehouseId || null,
        notes: form.notes.trim() || null,
        items: [{ productId: form.productId, quantity: form.quantity }]
      });
      setForm({ customerId: "", customerName: "", productId: "", warehouseId: "", quantity: 1, notes: "" });
      setNotice({ type: "success", message: "Sipariş oluşturuldu." });
      onChanged();
    } catch (error) {
      setNotice({ type: "error", message: getErrorMessage(error) });
    }
  }

  return (
    <div className="content-grid two-columns">
      <section className="tool-panel">
        <div className="section-title">
          <ClipboardCheck size={19} />
          <h2>Sipariş oluştur</h2>
        </div>
        <form className="form-grid" onSubmit={submit}>
          <label>
            Kayıtlı müşteri
            <select value={form.customerId} onChange={(event) => selectCustomer(event.target.value)}>
              <option value="">Serbest müşteri</option>
              {activeCustomers.map((customer) => (
                <option key={customer.id} value={customer.id}>{customer.code} - {customer.name}</option>
              ))}
            </select>
          </label>
          <label>
            Müşteri
            <input value={form.customerName} onChange={(event) => setForm({ ...form, customerName: event.target.value })} required />
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
              Depo
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
            Sipariş Oluştur
          </button>
        </form>
      </section>

      <OperationTable
        title="Siparişler"
        icon={<ClipboardCheck size={19} />}
        rows={orders.map((order) => ({
          id: order.id,
          number: order.orderNumber,
          party: order.customerName,
          warehouse: order.warehouseName || "-",
          status: order.status,
          quantity: order.totalQuantity,
          date: order.createdAt
        }))}
      />
    </div>
  );
}

function PurchaseRequestsView({
  api,
  products,
  warehouses,
  suppliers,
  purchaseRequests,
  onChanged,
  setNotice
}: {
  api: ReturnType<typeof createApiClient>;
  products: Product[];
  warehouses: Warehouse[];
  suppliers: Supplier[];
  purchaseRequests: PurchaseRequest[];
  onChanged: () => void;
  setNotice: (notice: Notice | null) => void;
}) {
  const [form, setForm] = useState({ supplierId: "", supplierName: "", productId: "", warehouseId: "", quantity: 1, notes: "" });
  const [receiveForm, setReceiveForm] = useState({ requestId: "", productId: "", quantity: 1 });
  const activeWarehouses = warehouses.filter((warehouse) => warehouse.isActive);
  const activeSuppliers = suppliers.filter((supplier) => supplier.isActive);
  const selectedWarehouseId = form.warehouseId || getDefaultWarehouseId(activeWarehouses);
  const requestPagination = usePagination(purchaseRequests);
  const receivableRequests = purchaseRequests.filter((request) =>
    (request.status === "Approved" || request.status === "PartiallyReceived") &&
    request.items.some((item) => item.quantity - (item.receivedQuantity ?? 0) > 0));
  const selectedReceiveRequest = purchaseRequests.find((request) => request.id === receiveForm.requestId);
  const receivableItems = selectedReceiveRequest?.items.filter((item) => item.quantity - (item.receivedQuantity ?? 0) > 0) ?? [];

  function selectSupplier(supplierId: string) {
    const supplier = activeSuppliers.find((item) => item.id === supplierId);
    setForm({ ...form, supplierId, supplierName: supplier?.name ?? form.supplierName });
  }

  function remainingPurchaseQuantity(item: OperationItem) {
    return item.quantity - (item.receivedQuantity ?? 0);
  }

  function selectReceiveRequest(requestId: string) {
    const request = purchaseRequests.find((item) => item.id === requestId);
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
        <div className="section-title">
          <ClipboardList size={19} />
          <h2>Alım talepleri</h2>
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
              {requestPagination.items.map((request) => (
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
          page={requestPagination.page}
          totalPages={requestPagination.totalPages}
          totalCount={requestPagination.totalCount}
          startIndex={requestPagination.startIndex}
          endIndex={requestPagination.endIndex}
          onPageChange={requestPagination.setPage}
        />
      </section>
    </div>
  );
}

function ShipmentsView({
  api,
  products,
  warehouses,
  customers,
  orders,
  shipments,
  onChanged,
  setNotice
}: {
  api: ReturnType<typeof createApiClient>;
  products: Product[];
  warehouses: Warehouse[];
  customers: Customer[];
  orders: SalesOrder[];
  shipments: Shipment[];
  onChanged: () => void;
  setNotice: (notice: Notice | null) => void;
}) {
  const [form, setForm] = useState({ salesOrderId: "", customerId: "", recipientName: "", productId: "", warehouseId: "", quantity: 1, trackingNumber: "", notes: "" });
  const activeWarehouses = warehouses.filter((warehouse) => warehouse.isActive);
  const activeCustomers = customers.filter((customer) => customer.isActive);
  const shippableOrders = orders.filter((order) =>
    order.status !== "Draft" &&
    order.status !== "Cancelled" &&
    order.items.some((item) => item.quantity - (item.shippedQuantity ?? 0) > 0));
  const selectedWarehouseId = form.warehouseId || getDefaultWarehouseId(activeWarehouses);

  function selectOrder(orderId: string) {
    const order = orders.find((item) => item.id === orderId);
    const orderItem = order?.items.find((item) => item.quantity - (item.shippedQuantity ?? 0) > 0);
    setForm({
      ...form,
      salesOrderId: orderId,
      customerId: order?.customerId ?? form.customerId,
      recipientName: order?.customerName ?? form.recipientName,
      warehouseId: order?.warehouseId ?? form.warehouseId,
      productId: orderItem?.productId ?? form.productId,
      quantity: orderItem ? orderItem.quantity - (orderItem.shippedQuantity ?? 0) : form.quantity
    });
  }

  function selectCustomer(customerId: string) {
    const customer = activeCustomers.find((item) => item.id === customerId);
    setForm({ ...form, customerId, recipientName: customer?.name ?? form.recipientName });
  }

  async function submit(event: FormEvent) {
    event.preventDefault();
    setNotice(null);
    try {
      await api.createShipment({
        salesOrderId: form.salesOrderId || null,
        customerId: form.customerId || null,
        recipientName: form.recipientName,
        warehouseId: selectedWarehouseId || null,
        trackingNumber: form.trackingNumber.trim() || null,
        notes: form.notes.trim() || null,
        items: [{ productId: form.productId, quantity: form.quantity }]
      });
      setForm({ salesOrderId: "", customerId: "", recipientName: "", productId: "", warehouseId: "", quantity: 1, trackingNumber: "", notes: "" });
      setNotice({ type: "success", message: "Sevkiyat oluşturuldu ve stok çıkışı işlendi." });
      onChanged();
    } catch (error) {
      setNotice({ type: "error", message: getErrorMessage(error) });
    }
  }

  return (
    <div className="content-grid two-columns">
      <section className="tool-panel">
        <div className="section-title">
          <Truck size={19} />
          <h2>Sevkiyat oluştur</h2>
        </div>
        <form className="form-grid" onSubmit={submit}>
          <label>
            Kayıtlı müşteri
            <select value={form.customerId} onChange={(event) => selectCustomer(event.target.value)}>
              <option value="">Serbest alıcı</option>
              {activeCustomers.map((customer) => (
                <option key={customer.id} value={customer.id}>{customer.code} - {customer.name}</option>
              ))}
            </select>
          </label>
          <label>
            Bağlı sipariş
            <select value={form.salesOrderId} onChange={(event) => selectOrder(event.target.value)}>
              <option value="">Bağımsız sevkiyat</option>
              {shippableOrders.map((order) => (
                <option key={order.id} value={order.id}>{order.orderNumber} - {order.customerName}</option>
              ))}
            </select>
          </label>
          <label>
            Alıcı
            <input value={form.recipientName} onChange={(event) => setForm({ ...form, recipientName: event.target.value })} required />
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
              Çıkış Deposu
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
            Takip No
            <input value={form.trackingNumber} onChange={(event) => setForm({ ...form, trackingNumber: event.target.value })} />
          </label>
          <button className="primary-action" type="submit">
            <Truck size={17} />
            Sevkiyat Oluştur
          </button>
        </form>
      </section>

      <OperationTable
        title="Sevkiyatlar"
        icon={<Truck size={19} />}
        rows={shipments.map((shipment) => ({
          id: shipment.id,
          number: shipment.shipmentNumber,
          party: shipment.recipientName,
          warehouse: shipment.warehouseName || "-",
          status: shipment.status,
          quantity: shipment.totalQuantity,
          date: shipment.shippedAt
        }))}
      />
    </div>
  );
}

function ReturnsView({
  api,
  products,
  warehouses,
  customers,
  orders,
  returns,
  onChanged,
  setNotice
}: {
  api: ReturnType<typeof createApiClient>;
  products: Product[];
  warehouses: Warehouse[];
  customers: Customer[];
  orders: SalesOrder[];
  returns: ReturnRequest[];
  onChanged: () => void;
  setNotice: (notice: Notice | null) => void;
}) {
  const [form, setForm] = useState({ salesOrderId: "", customerId: "", customerName: "", productId: "", warehouseId: "", quantity: 1, reason: "" });
  const activeWarehouses = warehouses.filter((warehouse) => warehouse.isActive);
  const activeCustomers = customers.filter((customer) => customer.isActive);
  const returnableOrders = orders.filter((order) =>
    order.status !== "Draft" &&
    order.status !== "Cancelled" &&
    order.items.some((item) => (item.shippedQuantity ?? 0) - (item.returnedQuantity ?? 0) > 0));
  const selectedWarehouseId = form.warehouseId || getDefaultWarehouseId(activeWarehouses);

  function selectOrder(orderId: string) {
    const order = orders.find((item) => item.id === orderId);
    const orderItem = order?.items.find((item) => (item.shippedQuantity ?? 0) - (item.returnedQuantity ?? 0) > 0);
    setForm({
      ...form,
      salesOrderId: orderId,
      customerId: order?.customerId ?? form.customerId,
      customerName: order?.customerName ?? form.customerName,
      warehouseId: order?.warehouseId ?? form.warehouseId,
      productId: orderItem?.productId ?? form.productId,
      quantity: orderItem ? (orderItem.shippedQuantity ?? 0) - (orderItem.returnedQuantity ?? 0) : form.quantity
    });
  }

  function selectCustomer(customerId: string) {
    const customer = activeCustomers.find((item) => item.id === customerId);
    setForm({ ...form, customerId, customerName: customer?.name ?? form.customerName });
  }

  async function submit(event: FormEvent) {
    event.preventDefault();
    setNotice(null);
    try {
      await api.createReturn({
        salesOrderId: form.salesOrderId || null,
        customerId: form.customerId || null,
        customerName: form.customerName,
        warehouseId: selectedWarehouseId || null,
        reason: form.reason,
        items: [{ productId: form.productId, quantity: form.quantity }]
      });
      setForm({ salesOrderId: "", customerId: "", customerName: "", productId: "", warehouseId: "", quantity: 1, reason: "" });
      setNotice({ type: "success", message: "İade kaydedildi ve stok girişi işlendi." });
      onChanged();
    } catch (error) {
      setNotice({ type: "error", message: getErrorMessage(error) });
    }
  }

  return (
    <div className="content-grid two-columns">
      <section className="tool-panel">
        <div className="section-title">
          <RotateCcw size={19} />
          <h2>İade kaydet</h2>
        </div>
        <form className="form-grid" onSubmit={submit}>
          <label>
            Kayıtlı müşteri
            <select value={form.customerId} onChange={(event) => selectCustomer(event.target.value)}>
              <option value="">Serbest müşteri</option>
              {activeCustomers.map((customer) => (
                <option key={customer.id} value={customer.id}>{customer.code} - {customer.name}</option>
              ))}
            </select>
          </label>
          <label>
            Bağlı sipariş
            <select value={form.salesOrderId} onChange={(event) => selectOrder(event.target.value)}>
              <option value="">Bağımsız iade</option>
              {returnableOrders.map((order) => (
                <option key={order.id} value={order.id}>{order.orderNumber} - {order.customerName}</option>
              ))}
            </select>
          </label>
          <label>
            Müşteri
            <input value={form.customerName} onChange={(event) => setForm({ ...form, customerName: event.target.value })} required />
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
              Giriş Deposu
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
            İade nedeni
            <textarea value={form.reason} onChange={(event) => setForm({ ...form, reason: event.target.value })} rows={3} required />
          </label>
          <button className="primary-action" type="submit">
            <RotateCcw size={17} />
            İade Kaydet
          </button>
        </form>
      </section>

      <OperationTable
        title="İadeler"
        icon={<RotateCcw size={19} />}
        rows={returns.map((item) => ({
          id: item.id,
          number: item.returnNumber,
          party: item.customerName,
          warehouse: item.warehouseName || "-",
          status: item.status,
          quantity: item.totalQuantity,
          date: item.receivedAt
        }))}
      />
    </div>
  );
}

function OperationTable({
  title,
  icon,
  rows
}: {
  title: string;
  icon: ReactNode;
  rows: Array<{ id: string; number: string; party: string; warehouse: string; status: string; quantity: number; date: string }>;
}) {
  const rowPagination = usePagination(rows);

  return (
    <section className="tool-panel">
      <div className="section-title">
        {icon}
        <h2>{title}</h2>
      </div>
      <div className="table-wrap">
        <table>
          <thead>
            <tr>
              <th>No</th>
              <th>Taraf</th>
              <th>Depo</th>
              <th>Adet</th>
              <th>Durum</th>
              <th>Tarih</th>
            </tr>
          </thead>
          <tbody>
            {rowPagination.items.map((row) => (
              <tr key={row.id}>
                <td>{row.number}</td>
                <td>{row.party}</td>
                <td>{row.warehouse}</td>
                <td>{row.quantity}</td>
                <td><span className={statusClass(row.status)}>{statusLabel(row.status)}</span></td>
                <td>{formatDate(row.date)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <PaginationControls
        page={rowPagination.page}
        totalPages={rowPagination.totalPages}
        totalCount={rowPagination.totalCount}
        startIndex={rowPagination.startIndex}
        endIndex={rowPagination.endIndex}
        onPageChange={rowPagination.setPage}
      />
    </section>
  );
}

function WarehousesView({
  api,
  products,
  warehouses,
  warehouseStock,
  onChanged,
  setNotice
}: {
  api: ReturnType<typeof createApiClient>;
  products: Product[];
  warehouses: Warehouse[];
  warehouseStock: WarehouseStock[];
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
  const warehousePagination = usePagination(warehouses);
  const warehouseStockPagination = usePagination(warehouseStock);

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
                  {warehouses.filter((warehouse) => warehouse.isActive).map((warehouse) => (
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
                  {warehouses.filter((warehouse) => warehouse.isActive).map((warehouse) => (
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
          <div className="section-title">
            <ClipboardList size={19} />
            <h2>Depolar</h2>
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
                {warehousePagination.items.map((warehouse) => (
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
            page={warehousePagination.page}
            totalPages={warehousePagination.totalPages}
            totalCount={warehousePagination.totalCount}
            startIndex={warehousePagination.startIndex}
            endIndex={warehousePagination.endIndex}
            onPageChange={warehousePagination.setPage}
          />
        </section>

        <section className="tool-panel">
          <div className="section-title">
            <Boxes size={19} />
            <h2>Depo stokları</h2>
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
                {warehouseStockPagination.items.map((item) => (
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
            page={warehouseStockPagination.page}
            totalPages={warehouseStockPagination.totalPages}
            totalCount={warehouseStockPagination.totalCount}
            startIndex={warehouseStockPagination.startIndex}
            endIndex={warehouseStockPagination.endIndex}
            onPageChange={warehouseStockPagination.setPage}
          />
        </section>
      </div>
    </div>
  );
}

function UsersView({
  api,
  users,
  onChanged,
  setNotice
}: {
  api: ReturnType<typeof createApiClient>;
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

function StockView({
  api,
  products,
  warehouses,
  movements,
  onChanged,
  setNotice
}: {
  api: ReturnType<typeof createApiClient>;
  products: Product[];
  warehouses: Warehouse[];
  movements: StockMovement[];
  onChanged: () => void;
  setNotice: (notice: Notice | null) => void;
}) {
  const [form, setForm] = useState({
    productId: "",
    warehouseId: "",
    type: "In" as StockMovementType,
    quantity: 1,
    reason: ""
  });
  const activeWarehouses = useMemo(() => warehouses.filter((warehouse) => warehouse.isActive), [warehouses]);
  const defaultWarehouseId = useMemo(() => getDefaultWarehouseId(activeWarehouses), [activeWarehouses]);
  const selectedWarehouseId = isActiveWarehouseId(activeWarehouses, form.warehouseId) ? form.warehouseId : defaultWarehouseId;

  useEffect(() => {
    if (form.warehouseId && !isActiveWarehouseId(activeWarehouses, form.warehouseId)) {
      setForm((current) => ({ ...current, warehouseId: defaultWarehouseId }));
    }
  }, [activeWarehouses, defaultWarehouseId, form.warehouseId]);

  async function submit(event: FormEvent) {
    event.preventDefault();
    setNotice(null);
    try {
      await api.createStockMovement({
        productId: form.productId,
        warehouseId: selectedWarehouseId || null,
        type: form.type,
        quantity: form.quantity,
        reason: form.reason.trim() || null
      });
      setForm({ productId: "", warehouseId: "", type: "In", quantity: 1, reason: "" });
      setNotice({ type: "success", message: "Stok hareketi işlendi." });
      onChanged();
    } catch (error) {
      setNotice({ type: "error", message: getErrorMessage(error) });
    }
  }

  return (
    <div className="content-grid two-columns">
      <section className="tool-panel">
        <div className="section-title">
          <Boxes size={19} />
          <h2>Stok hareketi</h2>
        </div>
        <form className="form-grid" onSubmit={submit}>
          <label>
            Ürün
            <select value={form.productId} onChange={(event) => setForm({ ...form, productId: event.target.value })} required>
              <option value="">Seç</option>
              {products.map((product) => (
                <option key={product.id} value={product.id}>
                  {product.sku} · {product.name}
                </option>
              ))}
            </select>
          </label>
          <label>
            Depo
            <select value={selectedWarehouseId} onChange={(event) => setForm({ ...form, warehouseId: event.target.value })}>
              <option value="">Varsayılan depo</option>
              {activeWarehouses.map((warehouse) => (
                <option key={warehouse.id} value={warehouse.id}>
                  {warehouse.code} · {warehouse.name}
                </option>
              ))}
            </select>
          </label>
          <div className="segmented-control" role="radiogroup" aria-label="Hareket tipi">
            {(["In", "Out", "Adjustment", "CountCorrection"] as StockMovementType[]).map((type) => (
              <button
                key={type}
                className={form.type === type ? "active" : ""}
                type="button"
                onClick={() => setForm({ ...form, type })}
              >
                {type === "In" ? "Giriş" : type === "Out" ? "Çıkış" : type === "Adjustment" ? "Düzeltme" : "Sayım"}
              </button>
            ))}
          </div>
          <label>
            Miktar
            <input type="number" min={0} value={form.quantity} onChange={(event) => setForm({ ...form, quantity: event.target.valueAsNumber })} />
          </label>
          <label>
            Açıklama
            <textarea value={form.reason} onChange={(event) => setForm({ ...form, reason: event.target.value })} rows={3} />
          </label>
          <button className="primary-action" type="submit">
            <Check size={17} />
            İşle
          </button>
        </form>
      </section>

      <section className="tool-panel">
        <div className="section-title">
          <ClipboardList size={19} />
          <h2>Hareket geçmişi</h2>
        </div>
        <div className="timeline-list">
          {movements.slice(0, 12).map((movement) => (
            <article className="timeline-item" key={movement.id}>
              <span className={`movement-dot ${movement.type.toLowerCase()}`} />
              <div>
                <strong>{movement.sku} · {movement.productName}</strong>
                <p>{movement.warehouseName || "Depo"} · {movement.type} · {movement.previousQuantity} → {movement.newQuantity}</p>
              </div>
              <time>{new Date(movement.createdAt).toLocaleString("tr-TR")}</time>
            </article>
          ))}
        </div>
      </section>
    </div>
  );
}

function CountView({
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
  api: ReturnType<typeof createApiClient>;
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

function BarcodeScanner({ onDetect }: { onDetect: (value: string) => void }) {
  const videoRef = useRef<HTMLVideoElement | null>(null);
  const controlsRef = useRef<IScannerControls | null>(null);
  const readerRef = useRef<BarcodeReader | null>(null);
  const [active, setActive] = useState(false);
  const [cameraMessage, setCameraMessage] = useState<string | null>(null);

  async function start() {
    setCameraMessage(null);

    if (!videoRef.current) {
      setCameraMessage("Kamera alanı hazırlanamadı. Sayfayı yenileyip tekrar deneyin.");
      return;
    }

    try {
      const { BrowserMultiFormatReader } = await import("@zxing/browser");
      const reader = readerRef.current ?? new BrowserMultiFormatReader();
      readerRef.current = reader;
      const controls = await reader.decodeFromVideoDevice(undefined, videoRef.current, (result) => {
        if (!result) {
          return;
        }

        onDetect(result.getText());
        stop();
      });
      controlsRef.current = controls;
      setActive(true);
    } catch {
      stop();
      setCameraMessage("Kamera açılamadı. Tarayıcı kamera iznini kontrol edin veya manuel barkod alanını kullanın.");
    }
  }

  function stop() {
    controlsRef.current?.stop();
    controlsRef.current = null;
    setActive(false);
  }

  useEffect(() => {
    return () => stop();
  }, []);

  return (
    <div className="scanner-box">
      <video ref={videoRef} muted playsInline />
      <button className={active ? "ghost-action" : "primary-action"} type="button" onClick={() => (active ? stop() : void start())}>
        <Camera size={17} />
        {active ? "Kamerayı kapat" : "Kamera"}
      </button>
      {cameraMessage && <span className="inline-warning">{cameraMessage}</span>}
    </div>
  );
}

function ReportsView({
  api,
  critical,
  movements,
  consistency,
  activeCount,
  setNotice
}: {
  api: ReturnType<typeof createApiClient>;
  critical: CriticalStock[];
  movements: StockMovement[];
  consistency: StockConsistency[];
  activeCount: InventoryCount | null;
  setNotice: (notice: Notice | null) => void;
}) {
  const movementPagination = usePagination(movements);
  const consistencyPagination = usePagination(consistency);

  async function exportFile(path: string, fileName: string) {
    try {
      await api.downloadExport(path, fileName);
    } catch (error) {
      setNotice({ type: "error", message: getErrorMessage(error) });
    }
  }

  return (
    <div className="content-grid">
      <section className="tool-panel">
        <div className="section-title spread">
          <span>
            <FileSpreadsheet size={19} />
            <h2>Excel dışa aktar</h2>
          </span>
        </div>
        <div className="export-grid">
          <button className="ghost-action" type="button" onClick={() => void exportFile("/exports/current-stock.xlsx", "stokio-current-stock.xlsx")}>
            <Download size={17} />
            Güncel stok
          </button>
          <button className="ghost-action" type="button" onClick={() => void exportFile("/exports/critical-stock.xlsx", "stokio-critical-stock.xlsx")}>
            <Download size={17} />
            Kritik stok
          </button>
          <button className="ghost-action" type="button" onClick={() => void exportFile("/exports/movements.xlsx", "stokio-stock-movements.xlsx")}>
            <Download size={17} />
            Hareketler
          </button>
          <button
            className="ghost-action"
            type="button"
            disabled={!activeCount}
            onClick={() => activeCount && void exportFile(`/exports/count-differences/${activeCount.id}.xlsx`, "stokio-count-differences.xlsx")}
          >
            <Download size={17} />
            Sayım farkı
          </button>
        </div>
      </section>

      <div className="content-grid two-columns">
        <section className="tool-panel">
        <div className="section-title">
          <AlertTriangle size={19} />
          <h2>Kritik stok</h2>
        </div>
        <div className="timeline-list">
          {critical.map((item) => (
            <article className="timeline-item" key={item.productId}>
              <span className="movement-dot out" />
              <div>
                <strong>{item.sku} · {item.productName}</strong>
                <p>{item.currentStock} / {item.criticalStockLevel}</p>
              </div>
            </article>
          ))}
        </div>
      </section>

      <section className="tool-panel">
        <div className="section-title">
          <BarChart3 size={19} />
          <h2>Son hareketler</h2>
        </div>
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Ürün</th>
                <th>Tip</th>
                <th>Miktar</th>
                <th>Son stok</th>
              </tr>
            </thead>
            <tbody>
              {movementPagination.items.map((movement) => (
                <tr key={movement.id}>
                  <td>{movement.sku}</td>
                  <td>{movement.type}</td>
                  <td>{movement.quantity}</td>
                  <td>{movement.newQuantity}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <PaginationControls
          page={movementPagination.page}
          totalPages={movementPagination.totalPages}
          totalCount={movementPagination.totalCount}
          startIndex={movementPagination.startIndex}
          endIndex={movementPagination.endIndex}
          onPageChange={movementPagination.setPage}
        />
      </section>
      </div>

      <section className="tool-panel">
        <div className="section-title">
          <ShieldCheck size={19} />
          <h2>Stok defteri tutarlılığı</h2>
        </div>
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>SKU</th>
                <th>Ürün</th>
                <th>Kayıtlı</th>
                <th>Defter</th>
                <th>Durum</th>
              </tr>
            </thead>
            <tbody>
              {consistencyPagination.items.map((item) => (
                <tr key={item.productId}>
                  <td>{item.sku}</td>
                  <td>{item.productName}</td>
                  <td>{item.storedCurrentStock}</td>
                  <td>{item.ledgerCurrentStock}</td>
                  <td>
                    <span className={item.isConsistent ? "pill" : "pill warn"}>
                      {item.isConsistent ? "Tutarlı" : `${item.issues.length} sorun`}
                    </span>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <PaginationControls
          page={consistencyPagination.page}
          totalPages={consistencyPagination.totalPages}
          totalCount={consistencyPagination.totalCount}
          startIndex={consistencyPagination.startIndex}
          endIndex={consistencyPagination.endIndex}
          onPageChange={consistencyPagination.setPage}
        />
      </section>
    </div>
  );
}

function Metric({ label, value, icon, tone }: { label: string; value: string; icon: ReactNode; tone?: "ok" | "warn" }) {
  return (
    <article className={`metric ${tone ?? ""}`}>
      <span>{icon}</span>
      <div>
        <strong>{value}</strong>
        <p>{label}</p>
      </div>
    </article>
  );
}

function TabButton({ active, onClick, icon, label }: { active: boolean; onClick: () => void; icon: ReactNode; label: string }) {
  return (
    <button className={active ? "active" : ""} onClick={onClick} type="button">
      <span className="nav-icon">{icon}</span>
      <span>{label}</span>
    </button>
  );
}

function containSidebarWheel(event: WheelEvent) {
  const element = event.currentTarget as HTMLElement | null;

  if (!element) {
    return;
  }

  const maxScrollTop = element.scrollHeight - element.clientHeight;

  event.stopPropagation();

  if (maxScrollTop <= 0) {
    event.preventDefault();
    return;
  }

  const nextScrollTop = element.scrollTop + event.deltaY;
  const isPastTop = event.deltaY < 0 && nextScrollTop <= 0;
  const isPastBottom = event.deltaY > 0 && nextScrollTop >= maxScrollTop;

  if (isPastTop || isPastBottom) {
    event.preventDefault();
    element.scrollTop = isPastTop ? 0 : maxScrollTop;
  }
}

function buildOperationTrend(
  orders: SalesOrder[],
  purchaseRequests: PurchaseRequest[],
  shipments: Shipment[],
  returns: ReturnRequest[]
) {
  const days = recentDayKeys(14);
  const counts = new Map(days.map((day) => [day.key, 0]));
  const dates = [
    ...orders.map((item) => item.createdAt),
    ...purchaseRequests.map((item) => item.createdAt),
    ...shipments.map((item) => item.shippedAt),
    ...returns.map((item) => item.receivedAt)
  ];

  for (const value of dates) {
    const key = toDateKey(value);
    if (counts.has(key)) {
      counts.set(key, (counts.get(key) ?? 0) + 1);
    }
  }

  return days.map((day) => ({ label: day.label, total: counts.get(day.key) ?? 0 }));
}

function buildStockFlow(movements: StockMovement[]) {
  const days = recentDayKeys(14);
  const flow = new Map(days.map((day) => [day.key, { inbound: 0, outbound: 0 }]));

  for (const movement of movements) {
    const key = toDateKey(movement.createdAt);
    const point = flow.get(key);
    if (!point) {
      continue;
    }

    if (movement.type === "In" || movement.type === "TransferIn") {
      point.inbound += movement.quantity;
    } else if (movement.type === "Out" || movement.type === "TransferOut") {
      point.outbound += movement.quantity;
    } else if (movement.newQuantity > movement.previousQuantity) {
      point.inbound += movement.newQuantity - movement.previousQuantity;
    } else if (movement.previousQuantity > movement.newQuantity) {
      point.outbound += movement.previousQuantity - movement.newQuantity;
    }
  }

  return days.map((day) => ({ label: day.label, ...flow.get(day.key)! }));
}

function buildWarehouseBars(warehouses: Warehouse[], warehouseStock: WarehouseStock[]) {
  const quantities = new Map<string, number>();
  for (const stock of warehouseStock) {
    quantities.set(stock.warehouseId, (quantities.get(stock.warehouseId) ?? 0) + stock.quantity);
  }

  return warehouses
    .filter((warehouse) => warehouse.isActive)
    .map((warehouse) => ({
      label: warehouse.name,
      value: quantities.get(warehouse.id) ?? warehouse.totalQuantity
    }))
    .sort((left, right) => right.value - left.value)
    .slice(0, 6);
}

function buildTopOperationProducts(
  orders: SalesOrder[],
  purchaseRequests: PurchaseRequest[],
  shipments: Shipment[],
  returns: ReturnRequest[]
) {
  const totals = new Map<string, { productId: string; sku: string; productName: string; quantity: number }>();
  const allItems = [
    ...orders.flatMap((item) => item.items),
    ...purchaseRequests.flatMap((item) => item.items),
    ...shipments.flatMap((item) => item.items),
    ...returns.flatMap((item) => item.items)
  ];

  for (const item of allItems) {
    const existing = totals.get(item.productId);
    if (existing) {
      existing.quantity += item.quantity;
    } else {
      totals.set(item.productId, {
        productId: item.productId,
        sku: item.sku,
        productName: item.productName,
        quantity: item.quantity
      });
    }
  }

  return [...totals.values()].sort((left, right) => right.quantity - left.quantity).slice(0, 6);
}

function buildRecentOperations(
  orders: SalesOrder[],
  purchaseRequests: PurchaseRequest[],
  shipments: Shipment[],
  returns: ReturnRequest[]
) {
  return [
    ...orders.map((item) => ({
      id: item.id,
      type: "Sipariş",
      number: item.orderNumber,
      party: item.customerName,
      quantity: item.totalQuantity,
      status: item.status,
      date: item.createdAt
    })),
    ...purchaseRequests.map((item) => ({
      id: item.id,
      type: "Alım",
      number: item.requestNumber,
      party: item.supplierName,
      quantity: item.totalQuantity,
      status: item.status,
      date: item.createdAt
    })),
    ...shipments.map((item) => ({
      id: item.id,
      type: "Sevkiyat",
      number: item.shipmentNumber,
      party: item.recipientName,
      quantity: item.totalQuantity,
      status: item.status,
      date: item.shippedAt
    })),
    ...returns.map((item) => ({
      id: item.id,
      type: "İade",
      number: item.returnNumber,
      party: item.customerName,
      quantity: item.totalQuantity,
      status: item.status,
      date: item.receivedAt
    }))
  ]
    .sort((left, right) => new Date(right.date).getTime() - new Date(left.date).getTime())
    .slice(0, 8);
}

function recentDayKeys(dayCount: number) {
  const today = startOfLocalDay(new Date());
  return Array.from({ length: dayCount }, (_, index) => {
    const date = new Date(today);
    date.setDate(today.getDate() - (dayCount - 1 - index));
    return {
      key: toDateKey(date.toISOString()),
      label: date.toLocaleDateString("tr-TR", { day: "2-digit", month: "2-digit" })
    };
  });
}

function toDateKey(value: string) {
  return startOfLocalDay(new Date(value)).toISOString().slice(0, 10);
}

function startOfLocalDay(date: Date) {
  return new Date(date.getFullYear(), date.getMonth(), date.getDate());
}

function getDefaultWarehouseId(warehouses: Warehouse[]) {
  return warehouses.find((warehouse) => warehouse.isDefault)?.id ?? warehouses[0]?.id ?? "";
}

function isActiveWarehouseId(warehouses: Warehouse[], warehouseId: string) {
  return Boolean(warehouseId) && warehouses.some((warehouse) => warehouse.id === warehouseId);
}

function findProductByBarcode(products: Product[], barcode: string) {
  const normalizedBarcode = barcode.trim();
  return products.find((product) =>
    product.isActive && product.barcodes.some((value) => value.trim() === normalizedBarcode));
}

function appendBarcode(currentValue: string, barcode: string) {
  const normalizedBarcode = barcode.trim();
  if (!normalizedBarcode) {
    return currentValue;
  }

  const existingBarcodes = currentValue
    .split(/\r?\n|,/)
    .map((value) => value.trim())
    .filter(Boolean);

  if (existingBarcodes.includes(normalizedBarcode)) {
    return existingBarcodes.join("\n");
  }

  return [...existingBarcodes, normalizedBarcode].join("\n");
}

function emptyToNull(value: string) {
  const trimmed = value.trim();
  return trimmed ? trimmed : null;
}

function statusClass(status: string) {
  return status === "Cancelled" || status === "Rejected" ? "pill warn" : "pill";
}

function statusLabel(status: string) {
  const labels: Record<string, string> = {
    Draft: "Taslak",
    Pending: "Bekliyor",
    PartiallyShipped: "Kısmi Sevk Edildi",
    Preparing: "Hazırlanıyor",
    Shipped: "Sevk Edildi",
    Completed: "Tamamlandı",
    Cancelled: "İptal",
    PendingApproval: "Onay Bekliyor",
    Approved: "Onaylandı",
    PartiallyReceived: "Kısmi Teslim Alındı",
    Received: "Teslim Alındı",
    Rejected: "Reddedildi"
  };

  return labels[status] ?? status;
}

function formatDate(value: string) {
  return new Date(value).toLocaleString("tr-TR");
}

function NoticeBox({ notice }: { notice: Notice }) {
  return <div className={`notice ${notice.type}`}>{notice.message}</div>;
}

function getErrorMessage(error: unknown) {
  if (!(error instanceof Error)) {
    return "İşlem tamamlanamadı.";
  }

  return translateApiError(error.message);
}

function translateApiError(message: string) {
  const exactTranslations: Record<string, string> = {
    "Barcode was not assigned to an active product.": "Bu barkod aktif bir ürüne tanımlı değil. Ürünler sayfasında barkodu ürüne ekleyin.",
    "Warehouse was not found.": "Depo bulunamadı. İşleme geçerli bir depo ile devam edin.",
    "Product was not found.": "Ürün bulunamadı.",
    "One or more products were not found.": "Bir veya daha fazla ürün bulunamadı.",
    "Customer was not found.": "Müşteri bulunamadı veya pasif durumda.",
    "Supplier was not found.": "Tedarikçi bulunamadı veya pasif durumda.",
    "Sales order was not found.": "Sipariş bulunamadı.",
    "Purchase request was not found.": "Alım talebi bulunamadı.",
    "Inventory count was not found.": "Sayım bulunamadı.",
    "Only open inventory counts can be changed.": "Sadece açık sayımlar değiştirilebilir.",
    "Only pending purchase requests can be approved.": "Sadece onay bekleyen alım talepleri onaylanabilir.",
    "Closed purchase requests cannot be received.": "Kapanmış alım talepleri teslim alınamaz.",
    "Stock cannot be reduced below zero.": "Stok sıfırın altına düşürülemez.",
    "Source warehouse stock is insufficient.": "Kaynak depodaki stok yetersiz.",
    "Source and target warehouses must be different.": "Kaynak ve hedef depo farklı olmalıdır.",
    "Default warehouse cannot be deactivated.": "Varsayılan depo pasife alınamaz.",
    "Warehouse with stock cannot be deactivated.": "İçinde stok bulunan depo pasife alınamaz.",
    "A warehouse with this code already exists.": "Bu koda sahip bir depo zaten var.",
    "A product with this SKU already exists.": "Bu SKU ile kayıtlı bir ürün zaten var.",
    "One or more barcodes are already assigned to a product.": "Barkodlardan biri veya daha fazlası başka bir ürüne atanmış.",
    "A category with this name already exists.": "Bu isimde bir kategori zaten var.",
    "A customer with this code already exists.": "Bu koda sahip bir müşteri zaten var.",
    "A supplier with this code already exists.": "Bu koda sahip bir tedarikçi zaten var.",
    "A user with this email already exists.": "Bu e-posta ile kayıtlı bir kullanıcı zaten var.",
    "Invalid tenant, email, or password.": "Tenant, e-posta veya şifre hatalı.",
    "This tenant slug is already in use.": "Bu tenant kısa adı zaten kullanılıyor.",
    "Tenant context is required.": "Tenant bağlamı gerekli."
  };

  if (exactTranslations[message]) {
    return exactTranslations[message];
  }

  return message
    .replaceAll("must not be empty", "boş olamaz")
    .replaceAll("must be greater than '0'", "0'dan büyük olmalıdır")
    .replaceAll("must be greater than or equal to '0'", "0 veya daha büyük olmalıdır")
    .replaceAll("is not a valid email address", "geçerli bir e-posta adresi değil");
}
