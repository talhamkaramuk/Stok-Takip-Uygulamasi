export function PaginationControls({
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
