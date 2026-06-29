import {
  Activity,
  AlertTriangle,
  Clock,
  Database,
  FileText,
  Search,
  ShieldCheck
} from "lucide-react";
import { useEffect, useState } from "react";
import type { ApiClient } from "../../shared/api/client";
import { getErrorMessage } from "../../shared/errors/getErrorMessage";
import { PaginationControls } from "../../shared/pagination/PaginationControls";
import { useServerPage } from "../../shared/pagination/useServerPage";
import type { Notice } from "../../shared/types/ui";
import { Metric } from "../../shared/ui/Metric";
import type { AuditLog, MetricsSnapshot } from "../../types";

export function AuditLogsView({
  api,
  setNotice
}: {
  api: ApiClient;
  setNotice: (notice: Notice | null) => void;
}) {
  const [metrics, setMetrics] = useState<MetricsSnapshot | null>(null);
  const [query, setQuery] = useState("");
  const [action, setAction] = useState("");
  const [entityName, setEntityName] = useState("");
  const auditPage = useServerPage<AuditLog, { search?: string; action?: string; entityName?: string }>({
    filters: {
      search: query.trim() || undefined,
      action: action.trim() || undefined,
      entityName: entityName.trim() || undefined
    },
    pageSize: 10,
    load: api.listAuditLogs,
    onError: (error) => setNotice({ type: "error", message: getErrorMessage(error) })
  });

  useEffect(() => {
    let cancelled = false;
    api.getMetrics()
      .then((snapshot) => {
        if (!cancelled) {
          setMetrics(snapshot);
        }
      })
      .catch((error) => {
        if (!cancelled) {
          setNotice({ type: "error", message: getErrorMessage(error) });
        }
      });

    return () => {
      cancelled = true;
    };
  }, [api, setNotice]);

  return (
    <div className="content-grid">
      <section className="metric-grid">
        <Metric label="Request" value={(metrics?.requestCount ?? 0).toString()} icon={<Activity size={19} />} />
        <Metric label="Ort. latency" value={`${metrics?.averageLatencyMs ?? 0} ms`} icon={<Clock size={19} />} />
        <Metric label="4xx / 5xx" value={`${metrics?.clientErrorCount ?? 0} / ${metrics?.serverErrorCount ?? 0}`} icon={<AlertTriangle size={19} />} tone={(metrics?.serverErrorCount ?? 0) > 0 ? "warn" : undefined} />
        <Metric label="Login başarılı" value={(metrics?.loginSuccessCount ?? 0).toString()} icon={<ShieldCheck size={19} />} tone="ok" />
        <Metric label="Stok hareketi" value={(metrics?.stockMovementCount ?? 0).toString()} icon={<FileText size={19} />} />
        <Metric label="Export hata" value={(metrics?.exportFailureCount ?? 0).toString()} icon={<Database size={19} />} tone={(metrics?.exportFailureCount ?? 0) > 0 ? "warn" : undefined} />
      </section>

      <section className="tool-panel">
        <div className="section-title spread">
          <span>
            <ShieldCheck size={19} />
            <h2>Audit logları</h2>
          </span>
          <div className="table-filter-row">
            <label className="search-field">
              <Search size={16} />
              <input value={query} onChange={(event) => setQuery(event.target.value)} placeholder="Action veya entity ara" />
            </label>
            <input value={action} onChange={(event) => setAction(event.target.value)} placeholder="Action" />
            <input value={entityName} onChange={(event) => setEntityName(event.target.value)} placeholder="Entity" />
          </div>
        </div>

        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Zaman</th>
                <th>Action</th>
                <th>Entity</th>
                <th>Kullanıcı</th>
                <th>Metadata</th>
              </tr>
            </thead>
            <tbody>
              {auditPage.items.map((log) => (
                <tr key={log.id}>
                  <td>{new Date(log.createdAt).toLocaleString("tr-TR")}</td>
                  <td><span className="pill">{log.action}</span></td>
                  <td>
                    <strong>{log.entityName}</strong>
                    <small className="muted-block">{log.entityId}</small>
                  </td>
                  <td>{log.userId ?? "-"}</td>
                  <td><code className="json-snippet">{compactJson(log.metadataJson ?? log.newValueJson ?? log.oldValueJson)}</code></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        <PaginationControls
          page={auditPage.page}
          totalPages={auditPage.totalPages}
          totalCount={auditPage.totalCount}
          startIndex={auditPage.startIndex}
          endIndex={auditPage.endIndex}
          onPageChange={auditPage.setPage}
        />
      </section>
    </div>
  );
}

function compactJson(value: string | null) {
  if (!value) {
    return "-";
  }

  try {
    return JSON.stringify(JSON.parse(value));
  } catch {
    return value;
  }
}
