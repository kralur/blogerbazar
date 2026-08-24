import { useCallback, useRef, useState } from "react";

export type CatalogFailure = "offline" | "server" | null;
export type CatalogPage<TItem> = { items: TItem[]; total: number; page: number; hasMore: boolean };

export function usePaginatedCatalog<TItem>({ active, fetchPage, getItemId = defaultItemId }: { active: boolean; fetchPage: (page: number, signal: AbortSignal) => Promise<CatalogPage<TItem>>; getItemId?: (item: TItem) => string }) {
  const [items, setItems] = useState<TItem[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(false);
  const [loadingMore, setLoadingMore] = useState(false);
  const [loadMoreFailed, setLoadMoreFailed] = useState(false);
  const [failure, setFailure] = useState<CatalogFailure>(null);
  const [page, setPage] = useState(1);
  const [hasMore, setHasMore] = useState(false);
  const [loadedInitialResult, setLoadedInitialResult] = useState(false);
  const requestIdRef = useRef(0);
  const abortControllerRef = useRef<AbortController>();
  const loadingPagesRef = useRef(new Set<number>());

  const cancel = useCallback(() => {
    requestIdRef.current += 1;
    abortControllerRef.current?.abort();
    loadingPagesRef.current.clear();
  }, []);

  const load = useCallback(async (requestedPage: number, append: boolean) => {
    if (!active || (append && loadingPagesRef.current.has(requestedPage))) return;
    if (!append) {
      abortControllerRef.current?.abort();
      loadingPagesRef.current.clear();
      setItems([]);
      setTotal(0);
      setPage(1);
      setHasMore(false);
    }

    const requestId = ++requestIdRef.current;
    const abortController = new AbortController();
    abortControllerRef.current = abortController;
    loadingPagesRef.current.add(requestedPage);

    if (append) {
      setLoadingMore(true);
      setLoadMoreFailed(false);
    } else {
      setLoading(true);
      setFailure(null);
      setLoadedInitialResult(false);
    }

    try {
      const result = await fetchPage(requestedPage, abortController.signal);
      if (requestId !== requestIdRef.current) return;
      setItems((current) => append ? [...current, ...result.items.filter((item) => !current.some((currentItem) => getItemId(currentItem) === getItemId(item)))] : result.items);
      setTotal(result.total);
      setPage(result.page);
      setHasMore(result.hasMore);
      setLoadedInitialResult(true);
    } catch (error) {
      if (abortController.signal.aborted || (error instanceof DOMException && error.name === "AbortError")) return;
      if (requestId !== requestIdRef.current) return;
      if (append) setLoadMoreFailed(true);
      else setFailure(navigator.onLine === false ? "offline" : "server");
    } finally {
      loadingPagesRef.current.delete(requestedPage);
      if (requestId !== requestIdRef.current) return;
      if (append) setLoadingMore(false);
      else setLoading(false);
    }
  }, [active, fetchPage]);

  return { items, total, loading, loadingMore, loadMoreFailed, failure, page, hasMore, loadedInitialResult, load, cancel };
}

function defaultItemId(item: unknown) {
  return (item as { id?: string }).id ?? "";
}
