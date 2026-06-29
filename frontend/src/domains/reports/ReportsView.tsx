import {
  AlertTriangle,
  BarChart3,
  Download,
  FileSpreadsheet,
  Loader2,
  ShieldCheck
} from "lucide-react";
import { useEffect, useState } from "react";
import type { ApiClient } from "../../shared/api/client";
import { getErrorMessage } from "../../shared/errors/getErrorMessage";
import { PaginationControls } from "../../shared/pagination/PaginationControls";
import { usePagination } from "../../shared/pagination/usePagination";
import { useServerPage } from "../../shared/pagination/useServerPage";
import type { Notice } from "../../shared/types/ui";
import type {
  CriticalStock,
  ExportJob,
  ExportJobType,
  InventoryCount,
  StockConsistency,
  StockMovement
} from "../../types";

const EXPORT_POLL_DELAY_MS = 800;
const EXPORT_MAX_POLLS = 30;

export function ReportsView({
  api,
  critical,
  activeCount,
  setNotice
}: {
  api: ApiClient;
  critical: CriticalStock[];
  activeCount: InventoryCount | null;
  setNotice: (notice: Notice | null) => void;
}) {
  const [consistency, setConsistency] = useState<StockConsistency[]>([]);
  const [exportingType, setExportingType] = useState<ExportJobType | null>(null);
  const movementPage = useServerPage<StockMovement, Record<string, never>>({
    filters: {},
    load: api.listStockMovements,
    onError: (error) => setNotice({ type: "error", message: getErrorMessage(error) })
  });
  const consistencyPagination = usePagination(consistency);

  useEffect(() => {
    let cancelled = false;
    api.checkStockConsistency()
      .then((items) => {
        if (!cancelled) {
          setConsistency(items);
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

  async function requestExport(type: ExportJobType, countId?: string | null) {
    try {
      setExportingType(type);
      const job = await api.createExportJob({ type, countId });
      const readyJob = await waitForExportJob(job);
      await api.downloadExportJob(readyJob.id, readyJob.fileName);
      setNotice({ type: "success", message: "Dışa aktarma dosyası hazırlandı." });
    } catch (error) {
      setNotice({ type: "error", message: getErrorMessage(error) });
    } finally {
      setExportingType(null);
    }
  }

  async function waitForExportJob(initialJob: ExportJob) {
    let job = initialJob;
    for (let attempt = 0; attempt < EXPORT_MAX_POLLS; attempt++) {
      if (job.status === "Ready") {
        return job;
      }

      if (job.status === "Failed") {
        throw new Error(job.errorMessage ?? "Dışa aktarma işlemi başarısız oldu.");
      }

      await sleep(EXPORT_POLL_DELAY_MS);
      job = await api.getExportJob(job.id);
    }

    throw new Error("Dışa aktarma işlemi beklenenden uzun sürdü. Biraz sonra tekrar deneyin.");
  }

  const exportDisabled = exportingType !== null;

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
          <button className="ghost-action" type="button" disabled={exportDisabled} onClick={() => void requestExport("CurrentStock")}>
            {exportingType === "CurrentStock" ? <Loader2 className="spin" size={17} /> : <Download size={17} />}
            Güncel stok
          </button>
          <button className="ghost-action" type="button" disabled={exportDisabled} onClick={() => void requestExport("CriticalStock")}>
            {exportingType === "CriticalStock" ? <Loader2 className="spin" size={17} /> : <Download size={17} />}
            Kritik stok
          </button>
          <button className="ghost-action" type="button" disabled={exportDisabled} onClick={() => void requestExport("StockMovements")}>
            {exportingType === "StockMovements" ? <Loader2 className="spin" size={17} /> : <Download size={17} />}
            Hareketler
          </button>
          <button
            className="ghost-action"
            type="button"
            disabled={!activeCount || exportDisabled}
            onClick={() => activeCount && void requestExport("CountDifferences", activeCount.id)}
          >
            {exportingType === "CountDifferences" ? <Loader2 className="spin" size={17} /> : <Download size={17} />}
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
                {movementPage.items.map((movement) => (
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
            page={movementPage.page}
            totalPages={movementPage.totalPages}
            totalCount={movementPage.totalCount}
            startIndex={movementPage.startIndex}
            endIndex={movementPage.endIndex}
            onPageChange={movementPage.setPage}
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

function sleep(durationMs: number) {
  return new Promise<void>((resolve) => window.setTimeout(resolve, durationMs));
}
