import {
  AlertTriangle,
  ArrowLeftRight,
  BarChart3,
  Boxes,
  Building2,
  Camera,
  Check,
  ClipboardCheck,
  ClipboardList,
  Download,
  FileSpreadsheet,
  Handshake,
  LogOut,
  PackagePlus,
  Plus,
  RotateCcw,
  RefreshCw,
  ScanLine,
  Search,
  ShieldCheck,
  Tags,
  Truck,
  Users
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

const tokenStorageKey = "stokio.accessToken";
const userStorageKey = "stokio.user";

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
  | "reports";

type Notice = {
  type: "success" | "error";
  message: string;
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
  reports: {
    title: "Raporlar",
    description: "Stok, kritik seviye, hareket ve sayım farkı raporlarını dışa aktarın."
  }
};

export default function App() {
  const [token, setToken] = useState<string | null>(() => localStorage.getItem(tokenStorageKey));
  const [user, setUser] = useState<AuthResponse["user"] | null>(() => {
    const raw = localStorage.getItem(userStorageKey);
    return raw ? (JSON.parse(raw) as AuthResponse["user"]) : null;
  });
  const [notice, setNotice] = useState<Notice | null>(null);
  const api = useMemo(() => createApiClient(token), [token]);

  function handleAuth(response: AuthResponse) {
    localStorage.setItem(tokenStorageKey, response.accessToken);
    localStorage.setItem(userStorageKey, JSON.stringify(response.user));
    setNotice(null);
    setToken(response.accessToken);
    setUser(response.user);
  }

  function logout() {
    localStorage.removeItem(tokenStorageKey);
    localStorage.removeItem(userStorageKey);
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
  const [form, setForm] = useState({
    businessName: "STOKIO Demo",
    tenantSlug: "stokio-demo",
    ownerName: "Talha",
    email: "owner@stokio.local",
    password: "StrongPass123"
  });

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
  const [tab, setTab] = useState<TabKey>("dashboard");
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
        nextReturns
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
        api.listReturns()
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
  }

  async function loadDifferences(countId: string) {
    setDifferences(await api.listCountDifferences(countId));
  }

  const totalStock = products.reduce((sum, product) => sum + product.currentStock, 0);
  const activeProducts = products.filter((product) => product.isActive).length;
  const page = tabMeta[tab];
  const initials = user.fullName
    .split(" ")
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase())
    .join("");

  return (
    <main className="app-shell">
      <aside className="sidebar" ref={sidebarRef}>
        <div className="brand-row">
          <div className="brand-mark compact">
            <Boxes size={20} />
          </div>
          <div className="brand-copy">
            <strong>STOKIO</strong>
            <span>Inventory OS</span>
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

        <button className="ghost-action logout" onClick={onLogout} type="button">
          <LogOut size={17} />
          Çıkış
        </button>
      </aside>

      <section className="workspace">
        <header className="topbar">
          <div className="tenant-context">
            <span className="eyebrow">Çalışma alanı</span>
            <strong>{user.tenantSlug}</strong>
          </div>
          <div className="topbar-actions">
            <div className="user-chip" aria-label="Aktif kullanıcı">
              <span>{initials || "ST"}</span>
              <div>
                <strong>{user.fullName}</strong>
                <small>{user.role}</small>
              </div>
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

        <section className="metric-grid">
          <Metric label="Aktif ürün" value={activeProducts.toString()} icon={<PackagePlus size={19} />} />
          <Metric label="Toplam stok" value={totalStock.toString()} icon={<Boxes size={19} />} />
          <Metric label="Sipariş" value={orders.length.toString()} icon={<ClipboardCheck size={19} />} />
          <Metric label="Sevkiyat" value={shipments.length.toString()} icon={<Truck size={19} />} />
          <Metric label="Kritik stok" value={critical.length.toString()} icon={<AlertTriangle size={19} />} tone={critical.length > 0 ? "warn" : "ok"} />
        </section>

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
        {tab === "orders" && (
          <OrdersView
            api={api}
            products={products}
            warehouses={warehouses}
            customers={customers}
            orders={orders}
            onChanged={() => void refresh()}
            setNotice={setNotice}
          />
        )}
        {tab === "purchase" && (
          <PurchaseRequestsView
            api={api}
            products={products}
            warehouses={warehouses}
            suppliers={suppliers}
            purchaseRequests={purchaseRequests}
            onChanged={() => void refresh()}
            setNotice={setNotice}
          />
        )}
        {tab === "shipments" && (
          <ShipmentsView
            api={api}
            products={products}
            warehouses={warehouses}
            customers={customers}
            orders={orders}
            shipments={shipments}
            onChanged={() => void refresh()}
            setNotice={setNotice}
          />
        )}
        {tab === "returns" && (
          <ReturnsView
            api={api}
            products={products}
            warehouses={warehouses}
            customers={customers}
            orders={orders}
            returns={returns}
            onChanged={() => void refresh()}
            setNotice={setNotice}
          />
        )}
        {tab === "warehouses" && (
          <WarehousesView
            api={api}
            products={products}
            warehouses={warehouses}
            warehouseStock={warehouseStock}
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
    { label: "Hazırlanan sipariş", value: orders.filter((order) => order.status === "Preparing").length },
    { label: "Onay bekleyen alım", value: purchaseRequests.filter((request) => request.status === "PendingApproval").length },
    { label: "Teslim alınacak alım", value: purchaseRequests.filter((request) => request.status === "Approved").length },
    { label: "Kritik stok", value: critical.length }
  ];
  const warehouseBars = buildWarehouseBars(warehouses, warehouseStock);
  const topProducts = buildTopOperationProducts(orders, purchaseRequests, shipments, returns);
  const recentOperations = buildRecentOperations(orders, purchaseRequests, shipments, returns);
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

      <section className="tool-panel dashboard-span-2">
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
              {recentOperations.map((item) => (
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
              {filteredProducts.map((product) => (
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
              {categories.map((category) => (
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
              {customers.map((customer) => (
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
              {suppliers.map((supplier) => (
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
  const activeWarehouses = warehouses.filter((warehouse) => warehouse.isActive);
  const activeSuppliers = suppliers.filter((supplier) => supplier.isActive);
  const selectedWarehouseId = form.warehouseId || getDefaultWarehouseId(activeWarehouses);

  function selectSupplier(supplierId: string) {
    const supplier = activeSuppliers.find((item) => item.id === supplierId);
    setForm({ ...form, supplierId, supplierName: supplier?.name ?? form.supplierName });
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
                <th>Adet</th>
                <th>Durum</th>
                <th>İşlem</th>
              </tr>
            </thead>
            <tbody>
              {purchaseRequests.map((request) => (
                <tr key={request.id}>
                  <td>{request.requestNumber}</td>
                  <td>{request.supplierName}</td>
                  <td>{request.warehouseName || "-"}</td>
                  <td>{request.totalQuantity}</td>
                  <td><span className={statusClass(request.status)}>{statusLabel(request.status)}</span></td>
                  <td>
                    <div className="table-actions">
                      {request.status === "PendingApproval" && (
                        <button className="ghost-action compact-action" type="button" onClick={() => void mutate(request.id, "approve")}>Onayla</button>
                      )}
                      {request.status !== "Received" && request.status !== "Cancelled" && (
                        <button className="primary-action compact-action" type="button" onClick={() => void mutate(request.id, "receive")}>Teslim Al</button>
                      )}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
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
  const selectedWarehouseId = form.warehouseId || getDefaultWarehouseId(activeWarehouses);

  function selectOrder(orderId: string) {
    const order = orders.find((item) => item.id === orderId);
    setForm({
      ...form,
      salesOrderId: orderId,
      customerId: order?.customerId ?? form.customerId,
      recipientName: order?.customerName ?? form.recipientName,
      warehouseId: order?.warehouseId ?? form.warehouseId,
      productId: order?.items[0]?.productId ?? form.productId,
      quantity: order?.items[0]?.quantity ?? form.quantity
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
              {orders.map((order) => (
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
  const selectedWarehouseId = form.warehouseId || getDefaultWarehouseId(activeWarehouses);

  function selectOrder(orderId: string) {
    const order = orders.find((item) => item.id === orderId);
    setForm({
      ...form,
      salesOrderId: orderId,
      customerId: order?.customerId ?? form.customerId,
      customerName: order?.customerName ?? form.customerName,
      warehouseId: order?.warehouseId ?? form.warehouseId,
      productId: order?.items[0]?.productId ?? form.productId,
      quantity: order?.items[0]?.quantity ?? form.quantity
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
              {orders.map((order) => (
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
            {rows.map((row) => (
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
                {warehouses.map((warehouse) => (
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
                {warehouseStock.map((item) => (
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
    password: "StrongPass123",
    role: "Staff" as "Manager" | "Staff"
  });

  async function submit(event: FormEvent) {
    event.preventDefault();
    setNotice(null);
    try {
      await api.createUser(form);
      setForm({ fullName: "", email: "", password: "StrongPass123", role: "Staff" });
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
              {users.map((managedUser) => (
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
              {differences.map((item) => (
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
              {movements.slice(0, 15).map((movement) => (
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
              {consistency.map((item) => (
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
    Preparing: "Hazırlanıyor",
    Shipped: "Sevk Edildi",
    Completed: "Tamamlandı",
    Cancelled: "İptal",
    PendingApproval: "Onay Bekliyor",
    Approved: "Onaylandı",
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
