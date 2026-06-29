import { useEffect, useMemo, useRef, useState } from "react";
import type { PagedResult } from "../../types";

const defaultPageSize = 8;

export function emptyPagedResult<T>(pageSize = defaultPageSize): PagedResult<T> {
  return {
    items: [],
    page: 1,
    pageSize,
    totalCount: 0,
    totalPages: 1
  };
}

export function useServerPage<T, TFilters extends object>({
  filters,
  load,
  pageSize = defaultPageSize,
  onError
}: {
  filters: TFilters;
  load: (query: TFilters & { page: number; pageSize: number }) => Promise<PagedResult<T>>;
  pageSize?: number;
  onError: (error: unknown) => void;
}) {
  const [page, setPage] = useState(1);
  const [refreshToken, setRefreshToken] = useState(0);
  const [result, setResult] = useState<PagedResult<T>>(() => emptyPagedResult<T>(pageSize));
  const filterKey = useMemo(() => JSON.stringify(filters), [filters]);
  const filterKeyRef = useRef(filterKey);
  const loadRef = useRef(load);
  const onErrorRef = useRef(onError);

  useEffect(() => {
    loadRef.current = load;
    onErrorRef.current = onError;
  }, [load, onError]);

  useEffect(() => {
    let cancelled = false;

    if (filterKeyRef.current !== filterKey) {
      filterKeyRef.current = filterKey;
      if (page !== 1) {
        setPage(1);
        return () => {
          cancelled = true;
        };
      }
    }

    const parsedFilters = JSON.parse(filterKey) as TFilters;

    loadRef.current({ ...parsedFilters, page, pageSize })
      .then((nextResult) => {
        if (!cancelled) {
          setResult(nextResult);
        }
      })
      .catch((error) => {
        if (!cancelled) {
          onErrorRef.current(error);
        }
      });

    return () => {
      cancelled = true;
    };
  }, [filterKey, page, pageSize, refreshToken]);

  return {
    result,
    items: result.items,
    page: result.page,
    pageSize: result.pageSize,
    totalCount: result.totalCount,
    totalPages: Math.max(1, result.totalPages),
    startIndex: result.totalCount === 0 ? 0 : (result.page - 1) * result.pageSize,
    endIndex: Math.min(result.page * result.pageSize, result.totalCount),
    setPage,
    reload: () => setRefreshToken((value) => value + 1)
  };
}
