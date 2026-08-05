import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { getBloggers, getCategories, type BloggerSearchFilters } from "../api/marketplace";
import { BloggerCard, type BloggerCardData } from "../components/BloggerCard";
import { BottomNav, BottomSheet, Button, EmptyState, Icon, SearchBar, Skeleton } from "../components/ui";
import { useDebouncedValue } from "../hooks/useDebouncedValue";
import { useScrollRestoration } from "../hooks/useScrollRestoration";
import { useProfileDataRefresh } from "../hooks/useProfileDataRefresh";
import { categoryLabel, cityLabel, useI18n } from "../i18n";
import { uzbekistanRegions } from "../lib/taxonomy";
import { useTelegram } from "../telegram/TelegramProvider";

const pageSize = 20;

export function BloggerSearch() {
  const { t } = useI18n();
  useScrollRestoration("search");
  const categoryFromHash = useMemo(() => new URLSearchParams(window.location.hash.split("?")[1] ?? "").get("category") ?? "", []);
  const [bloggers, setBloggers] = useState<BloggerCardData[]>([]);
  const [categories, setCategories] = useState<string[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(true);
  const [loadingMore, setLoadingMore] = useState(false);
  const [loadMoreFailed, setLoadMoreFailed] = useState(false);
  const [failed, setFailed] = useState(false);
  const [filtersOpen, setFiltersOpen] = useState(false);
  const [query, setQuery] = useState("");
  const [filters, setFilters] = useState<BloggerSearchFilters>({ category: categoryFromHash, sort: "popular", pageSize });
  const [page, setPage] = useState(1);
  const [hasMore, setHasMore] = useState(false);
  const sentinelRef = useRef<HTMLDivElement | null>(null);
  const requestIdRef = useRef(0);
  const abortControllerRef = useRef<AbortController>();
  const debouncedQuery = useDebouncedValue(query, 300);

  const load = useCallback(async (requestedPage: number, append: boolean) => {
    const requestId = ++requestIdRef.current;
    abortControllerRef.current?.abort();
    const abortController = new AbortController();
    abortControllerRef.current = abortController;
    if (append) {
      setLoadingMore(true);
      setLoadMoreFailed(false);
    }
    else {
      setLoading(true);
      setFailed(false);
    }

    try {
      const result = await getBloggers({ ...filters, query: debouncedQuery || undefined, page: requestedPage, pageSize }, abortController.signal);
      if (requestId !== requestIdRef.current) return;
      setBloggers((current) => append
        ? [...current, ...result.bloggers.filter((blogger) => !current.some((item) => item.id === blogger.id))]
        : result.bloggers);
      setTotal(result.total);
      setPage(result.page);
      setHasMore(result.page * result.pageSize < result.total);
    } catch (error) {
      if (abortController.signal.aborted || (error instanceof DOMException && error.name === "AbortError")) return;
      if (requestId !== requestIdRef.current) return;
      if (append) setLoadMoreFailed(true);
      else setFailed(true);
    } finally {
      if (requestId !== requestIdRef.current) return;
      if (append) setLoadingMore(false);
      else setLoading(false);
    }
  }, [debouncedQuery, filters]);

  useEffect(() => {
    void getCategories().then(setCategories).catch(() => undefined);
  }, []);

  useEffect(() => {
    void load(1, false);
    return () => {
      requestIdRef.current += 1;
      abortControllerRef.current?.abort();
    };
  }, [load]);
  const refreshProfileData = useCallback(() => { void load(1, false); }, [load]);
  useProfileDataRefresh(refreshProfileData);

  useEffect(() => {
    const sentinel = sentinelRef.current;
    if (!sentinel || loading || loadingMore || loadMoreFailed || !hasMore || failed) return;
    const observer = new IntersectionObserver(([entry]) => {
      if (entry.isIntersecting) void load(page + 1, true);
    }, { rootMargin: "240px" });
    observer.observe(sentinel);
    return () => observer.disconnect();
  }, [failed, hasMore, load, loadMoreFailed, loading, loadingMore, page]);

  const select = (key: keyof BloggerSearchFilters, value: string | number | undefined) => {
    setFilters((current) => ({ ...current, [key]: value || undefined }));
  };

  return <div className="screen screen--with-nav space-y-4 px-4 pt-5">
    <header className="flex items-center justify-between"><div><p className="text-sm font-semibold text-brand-muted">{t("search.eyebrow")}</p><h1 className="mt-1 text-3xl font-extrabold tracking-tight">{t("search.title")}</h1></div><button aria-expanded={filtersOpen} aria-label={t("search.filters")} className="grid h-11 w-11 place-items-center rounded-2xl bg-white shadow-card" onClick={() => setFiltersOpen((value) => !value)} type="button"><Icon name="filter" /></button></header>
    <SearchBar onChange={(event) => setQuery(event.target.value)} placeholder={t("search.placeholder")} value={query} />
    <BottomSheet onClose={() => setFiltersOpen(false)} open={filtersOpen} title={t("search.filters")}><div className="grid gap-3"><FilterSelect label={t("common.categories")} onChange={(value) => select("category", value)} options={[["", t("common.all")], ...categories.map((category) => [category, categoryLabel(category)])]} value={filters.category ?? ""} /><FilterSelect label={t("common.city")} onChange={(value) => select("city", value)} options={[["", t("common.any")], ...uzbekistanRegions.map((city) => [city, cityLabel(city)])]} value={filters.city ?? ""} /><FilterSelect label={t("search.platform")} onChange={(value) => select("platform", value)} options={[["", t("common.all")], ["instagram", t("search.platformInstagram")], ["telegram", t("search.platformTelegram")], ["tiktok", t("search.platformTiktok")], ["youtube", t("search.platformYoutube")]]} value={filters.platform ?? ""} /><FilterSelect label={t("search.followers")} onChange={(value) => select("minFollowers", Number(value) || undefined)} options={[["", t("common.all")], ["10000", "10 000+"], ["50000", "50 000+"], ["100000", "100 000+"]]} value={String(filters.minFollowers ?? "")} /><FilterSelect label={t("search.er")} onChange={(value) => select("minEr", Number(value) || undefined)} options={[["", t("common.all")], ["3", "3%+"], ["5", "5%+"], ["8", "8%+"]]} value={String(filters.minEr ?? "")} /><FilterSelect label={t("search.maxPrice")} onChange={(value) => select("maxPrice", Number(value) || undefined)} options={[["", t("search.anyPrice")], ["300000", `${t("common.to")} 300 000`], ["500000", `${t("common.to")} 500 000`], ["1000000", `${t("common.to")} 1 000 000`]]} value={String(filters.maxPrice ?? "")} /><FilterSelect label={t("search.sort")} onChange={(value) => select("sort", value)} options={[["popular", t("search.sortPopular")], ["rating", t("search.sortRating")], ["er", t("search.sortEr")], ["price", t("search.sortPrice")], ["newest", t("search.sortNewest")]]} value={filters.sort ?? "popular"} /><Button className="w-full" onClick={() => setFiltersOpen(false)} type="button">{t("common.apply")}</Button></div></BottomSheet>
    <p aria-live="polite" className="text-sm text-brand-muted">{loading ? t("search.loading") : t("search.found", { count: total })}</p>
    <div aria-busy={loading || loadingMore} className="grid gap-3">
      {loading ? [1, 2, 3].map((item) => <Skeleton className="h-48" key={item} />) : failed ? <div className="space-y-3"><EmptyState icon="filter" subtitle={t("search.loadFailedSubtitle")} title={t("search.loadFailedTitle")} /><Button className="w-full" onClick={() => void load(1, false)} type="button">{t("common.retry")}</Button></div> : bloggers.length ? bloggers.map((blogger) => <BloggerCard blogger={blogger} key={blogger.id} />) : <EmptyState subtitle={t("search.emptySubtitle")} title={t("search.emptyTitle")} />}
      {!loading && !failed && bloggers.length > 0 && <><div aria-hidden="true" ref={sentinelRef} />{loadingMore && [1, 2].map((item) => <Skeleton className="h-48" key={`loading-${item}`} />)}{loadMoreFailed && <div className="space-y-3"><EmptyState icon="filter" subtitle={t("search.loadFailedSubtitle")} title={t("search.loadFailedTitle")} /><Button className="w-full" onClick={() => void load(page + 1, true)} type="button">{t("common.retry")}</Button></div>}</>}
    </div>
    <BottomNav />
  </div>;
}

function FilterSelect({ label, value, options, onChange }: { label: string; value: string; options: string[][]; onChange: (value: string) => void }) {
  const { haptic } = useTelegram();
  return <label className="grid gap-2"><span className="text-[13px] font-bold text-brand-muted">{label}</span><select className="h-[52px] rounded-2xl border border-brand-line bg-white px-3 text-sm font-semibold outline-none focus:border-brand-blue focus:ring-4 focus:ring-blue-100" onChange={(event) => { haptic.selection(); onChange(event.target.value); }} value={value}>{options.map(([optionValue, optionLabel]) => <option key={optionValue} value={optionValue}>{optionLabel}</option>)}</select></label>;
}
