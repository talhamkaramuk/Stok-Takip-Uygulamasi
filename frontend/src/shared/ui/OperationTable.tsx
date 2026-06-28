import type { ReactNode } from "react";
import { PaginationControls } from "../pagination/PaginationControls";
import { usePagination } from "../pagination/usePagination";
import { formatDate, statusClass, statusLabel } from "../utils/inventory";

export function OperationTable({
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
