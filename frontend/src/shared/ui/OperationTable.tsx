import type { ReactNode } from "react";
import { PaginationControls } from "../pagination/PaginationControls";
import { usePagination } from "../pagination/usePagination";
import { formatDate, statusClass, statusLabel } from "../utils/inventory";

export type TablePagination = {
  page: number;
  totalPages: number;
  totalCount: number;
  startIndex: number;
  endIndex: number;
  onPageChange: (page: number) => void;
};

export function OperationTable({
  title,
  icon,
  actions,
  rows,
  pagination
}: {
  title: string;
  icon: ReactNode;
  actions?: ReactNode;
  rows: Array<{ id: string; number: string; party: string; warehouse: string; status: string; quantity: number; date: string }>;
  pagination?: TablePagination;
}) {
  const rowPagination = usePagination(rows);
  const tablePagination = pagination ?? {
    page: rowPagination.page,
    totalPages: rowPagination.totalPages,
    totalCount: rowPagination.totalCount,
    startIndex: rowPagination.startIndex,
    endIndex: rowPagination.endIndex,
    onPageChange: rowPagination.setPage
  };
  const displayRows = pagination ? rows : rowPagination.items;

  return (
    <section className="tool-panel">
      <div className={actions ? "section-title spread" : "section-title"}>
        {actions ? (
          <>
            <span>
              {icon}
              <h2>{title}</h2>
            </span>
            {actions}
          </>
        ) : (
          <>
            {icon}
            <h2>{title}</h2>
          </>
        )}
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
            {displayRows.map((row) => (
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
        page={tablePagination.page}
        totalPages={tablePagination.totalPages}
        totalCount={tablePagination.totalCount}
        startIndex={tablePagination.startIndex}
        endIndex={tablePagination.endIndex}
        onPageChange={tablePagination.onPageChange}
      />
    </section>
  );
}
