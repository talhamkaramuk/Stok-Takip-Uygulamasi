import { useEffect, useState } from "react";

export function usePagination<T>(items: T[], pageSize = 8) {
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
