import {
  AlertTriangle,
  ArrowLeftRight,
  BarChart3,
  Boxes,
  ClipboardCheck,
  ClipboardList,
  PackagePlus,
  Users
} from "lucide-react";
import { PaginationControls } from "../../shared/pagination/PaginationControls";
import { usePagination } from "../../shared/pagination/usePagination";
import { formatDate, statusClass, statusLabel } from "../../shared/utils/inventory";
import type { DashboardSummary } from "../../types";

export function DashboardView({ summary }: { summary: DashboardSummary | null }) {
  const operationTrend = summary?.operationTrend ?? [];
  const stockFlow = summary?.stockFlow ?? [];
  const operationBars = summary?.operationBars ?? [];
  const pendingJobs = summary?.pendingJobs ?? [];
  const warehouseBars = summary?.warehouseBars ?? [];
  const topProducts = summary?.topProducts ?? [];
  const recentOperations = summary?.recentOperations ?? [];
  const recentOperationPagination = usePagination(recentOperations);
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
            <strong>{summary?.activeCustomerCount ?? 0}</strong>
            <span>Aktif müşteri</span>
          </div>
          <div>
            <strong>{summary?.activeSupplierCount ?? 0}</strong>
            <span>Aktif tedarikçi</span>
          </div>
          <div>
            <strong>{summary?.activeProductCount ?? 0}</strong>
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

function HorizontalBars({ rows }: { rows: Array<{ label: string; value: number; tone?: string | null }> }) {
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
