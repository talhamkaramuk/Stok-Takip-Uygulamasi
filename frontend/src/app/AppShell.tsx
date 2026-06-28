import {
  BarChart3,
  Boxes,
  Building2,
  ChevronDown,
  ClipboardCheck,
  Download,
  Handshake,
  LogOut,
  Menu,
  PackagePlus,
  PanelLeftClose,
  PanelLeftOpen,
  RefreshCw,
  RotateCcw,
  ScanLine,
  ShieldCheck,
  Tags,
  Truck,
  UserCircle,
  Users,
  X
} from "lucide-react";
import { useEffect, useRef, useState } from "react";
import { CountView } from "../domains/counts/CountView";
import { CustomersView } from "../domains/customers/CustomersView";
import { DashboardView } from "../domains/dashboard/DashboardView";
import { OrdersView } from "../domains/orders/OrdersView";
import { CategoriesView } from "../domains/products/CategoriesView";
import { ProductsView } from "../domains/products/ProductsView";
import { PurchaseRequestsView } from "../domains/purchase/PurchaseRequestsView";
import { ReportsView } from "../domains/reports/ReportsView";
import { ReturnsView } from "../domains/returns/ReturnsView";
import { ShipmentsView } from "../domains/shipments/ShipmentsView";
import { StockView } from "../domains/stock/StockView";
import { SuppliersView } from "../domains/suppliers/SuppliersView";
import { UsersView } from "../domains/users/UsersView";
import { WarehousesView } from "../domains/warehouses/WarehousesView";
import type { ApiClient } from "../shared/api/client";
import { getErrorMessage } from "../shared/errors/getErrorMessage";
import type { Notice, TabKey } from "../shared/types/ui";
import { Metric } from "../shared/ui/Metric";
import { NoticeBox } from "../shared/ui/NoticeBox";
import { TabButton } from "../shared/ui/TabButton";
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
  Supplier,
  Warehouse,
  WarehouseStock
} from "../types";
import { tabMeta } from "./navigation";
import { buildPageMetrics } from "./pageMetrics";
import { containSidebarWheel } from "./sidebarScroll";

export function AppShell({
  api,
  user,
  onLogout,
  notice,
  setNotice
}: {
  api: ApiClient;
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