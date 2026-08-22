import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { getBloggers, getCategories, type BloggerSearchFilters } from "../api/marketplace";
import { BloggerCard, type BloggerCardData } from "../components/BloggerCard";
import { LanguageSwitcher } from "../components/LanguageSwitcher";
import { BottomNav, BottomSheet, Icon, SearchBar, Skeleton } from "../components/ui";
import { useDebouncedValue } from "../hooks/useDebouncedValue";
import { useProfileDataRefresh } from "../hooks/useProfileDataRefresh";
import { useScrollRestoration } from "../hooks/useScrollRestoration";
import { categoryLabel, cityLabel, useI18n } from "../i18n";
import { formatCurrency, formatNumber, formatPercentage } from "../lib/currency";
import { uzbekistanRegions } from "../lib/taxonomy";
import { useTelegram } from "../telegram/TelegramProvider";

const pageSize = 20;
const defaultFilters: BloggerSearchFilters = { sort: "popular", pageSize };
const filterSheetId = "blogger-search-filters";

type FilterKey = "category" | "city" | "platform" | "minFollowers" | "minEr" | "maxPrice" | "sort";
type SearchFailure = "offline" | "server" | null;
type Translate = (key: string, values?: Record<string, string | number>) => string;

function hashCategory() {
  const hash = window.location.hash;
  if (!hash.startsWith("#/search")) return null;
  return new URLSearchParams(hash.split("?")[1] ?? "").get("category");
}

function normalizedFilters(filters: BloggerSearchFilters): BloggerSearchFilters {
  return {
    ...defaultFilters,
    ...Object.fromEntries(Object.entries(filters).filter(([, value]) => value !== "" && value !== undefined)),
    pageSize
  };
}

function filtersEqual(left: BloggerSearchFilters, right: BloggerSearchFilters) {
  const keys = new Set([...Object.keys(left), ...Object.keys(right)]);
  return [...keys].every((key) => left[key as keyof BloggerSearchFilters] === right[key as keyof BloggerSearchFilters]);
}

export function BloggerSearch() {
  const { t } = useI18n();
  const { haptic } = useTelegram();
  useScrollRestoration("search");
  const initialCategory = hashCategory() ?? "";
  const [bloggers, setBloggers] = useState<BloggerCardData[]>([]);
  const [categories, setCategories] = useState<string[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(true);
  const [loadingMore, setLoadingMore] = useState(false);
  const [loadMoreFailed, setLoadMoreFailed] = useState(false);
  const [failure, setFailure] = useState<SearchFailure>(null);
  const [filtersOpen, setFiltersOpen] = useState(false);
  const [query, setQuery] = useState("");
  const [appliedFilters, setAppliedFilters] = useState<BloggerSearchFilters>(() => normalizedFilters({ category: initialCategory }));
  const [draftFilters, setDraftFilters] = useState<BloggerSearchFilters>(() => normalizedFilters({ category: initialCategory }));
  const draftFiltersRef = useRef<BloggerSearchFilters>(normalizedFilters({ category: initialCategory }));
  const [page, setPage] = useState(1);
  const [hasMore, setHasMore] = useState(false);
  const [loadedInitialResult, setLoadedInitialResult] = useState(false);
  const sentinelRef = useRef<HTMLDivElement | null>(null);
  const requestIdRef = useRef(0);
  const abortControllerRef = useRef<AbortController>();
  const loadingPagesRef = useRef(new Set<number>());
  const debouncedQuery = useDebouncedValue(query, 300);

  const load = useCallback(async (requestedPage: number, append: boolean) => {
    if (append && loadingPagesRef.current.has(requestedPage)) return;
    if (!append) {
      abortControllerRef.current?.abort();
      loadingPagesRef.current.clear();
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
      const result = await getBloggers({ ...appliedFilters, query: debouncedQuery || undefined, page: requestedPage, pageSize }, abortController.signal);
      if (requestId !== requestIdRef.current) return;
      setBloggers((current) => append
        ? [...current, ...result.bloggers.filter((blogger) => !current.some((item) => item.id === blogger.id))]
        : result.bloggers);
      setTotal(result.total);
      setPage(result.page);
      setHasMore(result.page * result.pageSize < result.total);
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
  }, [appliedFilters, debouncedQuery]);

  const refresh = useCallback(() => { void load(1, false); }, [load]);

  useEffect(() => {
    void getCategories().then(setCategories).catch(() => undefined);
  }, []);

  useEffect(() => {
    void load(1, false);
    return () => {
      requestIdRef.current += 1;
      abortControllerRef.current?.abort();
      loadingPagesRef.current.clear();
    };
  }, [load]);

  useProfileDataRefresh(refresh);

  useEffect(() => {
    const syncHashCategory = () => {
      const category = hashCategory();
      if (category === null) return;
      setAppliedFilters((current) => {
        const next = normalizedFilters({ ...current, category: category || undefined });
        return filtersEqual(current, next) ? current : next;
      });
      const nextDraft = normalizedFilters({ ...draftFiltersRef.current, category: category || undefined });
      draftFiltersRef.current = nextDraft;
      setDraftFilters(nextDraft);
    };
    window.addEventListener("hashchange", syncHashCategory);
    return () => window.removeEventListener("hashchange", syncHashCategory);
  }, []);

  useEffect(() => {
    const sentinel = sentinelRef.current;
    if (!sentinel || loading || loadingMore || loadMoreFailed || !hasMore || failure) return;
    const observer = new IntersectionObserver(([entry]) => {
      if (entry.isIntersecting) void load(page + 1, true);
    }, { rootMargin: "240px" });
    observer.observe(sentinel);
    return () => observer.disconnect();
  }, [failure, hasMore, load, loadMoreFailed, loading, loadingMore, page]);

  const setDraft = (key: FilterKey, value: string | number | undefined) => {
    const next = normalizedFilters({ ...draftFiltersRef.current, [key]: value || undefined });
    draftFiltersRef.current = next;
    setDraftFilters(next);
  };

  const openFilters = () => {
    draftFiltersRef.current = appliedFilters;
    setDraftFilters(appliedFilters);
    setFiltersOpen(true);
  };

  const applyFilters = () => {
    haptic.selection();
    setAppliedFilters(normalizedFilters(draftFiltersRef.current));
    setFiltersOpen(false);
  };

  const resetFilters = () => {
    haptic.selection();
    const reset = normalizedFilters({});
    draftFiltersRef.current = reset;
    setDraftFilters(reset);
    setAppliedFilters(reset);
    setFiltersOpen(false);
  };

  const removeFilter = (key: FilterKey) => {
    haptic.selection();
    setAppliedFilters((current) => normalizedFilters({ ...current, [key]: undefined }));
    const nextDraft = normalizedFilters({ ...draftFiltersRef.current, [key]: undefined });
    draftFiltersRef.current = nextDraft;
    setDraftFilters(nextDraft);
  };

  const changeSort = (sort: NonNullable<BloggerSearchFilters["sort"]>) => {
    haptic.selection();
    setAppliedFilters((current) => normalizedFilters({ ...current, sort }));
    const nextDraft = normalizedFilters({ ...draftFiltersRef.current, sort });
    draftFiltersRef.current = nextDraft;
    setDraftFilters(nextDraft);
  };

  const activeChips = useMemo(() => buildActiveChips(appliedFilters, t), [appliedFilters, t]);

  return <div className="catalog-search screen screen--with-nav">
    <header className="catalog-search__header">
      <div className="catalog-search__heading"><p className="catalog-search__eyebrow">{t("search.eyebrow")}</p><h1>{t("search.title")}</h1></div>
      <LanguageSwitcher />
    </header>
    <div className="catalog-search__searchbar"><SearchBar className="catalog-search__search-control" onChange={(event) => setQuery(event.target.value)} placeholder={t("search.placeholder")} value={query} /></div>
    <div className="catalog-search__controls">
      <button aria-controls={filterSheetId} aria-expanded={filtersOpen} aria-label={t("search.filters")} className="catalog-search__filter-button" onClick={openFilters} type="button"><Icon name="filter" /><span>{t("search.filters")}</span></button>
      <label className="catalog-search__sort"><span>{t("search.sort")}</span><select aria-label={t("search.sort")} onChange={(event) => changeSort(event.target.value as NonNullable<BloggerSearchFilters["sort"]>)} value={appliedFilters.sort ?? "popular"}>{sortOptions(t).map(([value, label]) => <option key={value} value={value}>{label}</option>)}</select></label>
    </div>
    <ActiveFilterChips chips={activeChips} onRemove={removeFilter} onReset={resetFilters} />
    <BottomSheet id={filterSheetId} onClose={() => setFiltersOpen(false)} open={filtersOpen} title={t("search.filters")}><div className="catalog-search__sheet-content">
      <FilterSelect label={t("common.categories")} onChange={(value) => setDraft("category", value)} options={[["", t("common.all")], ...categories.map((category) => [category, safeCategoryLabel(category, t)])]} value={draftFilters.category ?? ""} />
      <FilterSelect label={t("common.city")} onChange={(value) => setDraft("city", value)} options={[["", t("common.any")], ...uzbekistanRegions.map((city) => [city, cityLabel(city)])]} value={draftFilters.city ?? ""} />
      <FilterSelect label={t("search.platform")} onChange={(value) => setDraft("platform", value)} options={platformOptions(t)} value={draftFilters.platform ?? ""} />
      <FilterSelect label={t("search.followers")} onChange={(value) => setDraft("minFollowers", Number(value) || undefined)} options={followersOptions(t)} value={String(draftFilters.minFollowers ?? "")} />
      <FilterSelect label={t("search.er")} onChange={(value) => setDraft("minEr", Number(value) || undefined)} options={erOptions(t)} value={String(draftFilters.minEr ?? "")} />
      <FilterSelect label={t("search.maxPrice")} onChange={(value) => setDraft("maxPrice", Number(value) || undefined)} options={priceOptions(t)} value={String(draftFilters.maxPrice ?? "")} />
      <FilterSelect label={t("search.sort")} onChange={(value) => setDraft("sort", value as NonNullable<BloggerSearchFilters["sort"]>)} options={sortOptions(t)} value={draftFilters.sort ?? "popular"} />
      <div className="catalog-search__sheet-actions"><button className="catalog-search__secondary-button" onClick={resetFilters} type="button">{t("common.reset")}</button><button className="catalog-search__primary-button" onClick={applyFilters} type="button">{t("common.apply")}</button></div>
    </div></BottomSheet>
    <p aria-live="polite" className="catalog-search__results-count">{loading ? t("search.loading") : t("search.found", { count: total })}</p>
    <section aria-busy={loading || loadingMore} aria-live="polite" className="catalog-search__results">
      {loading ? <SearchSkeleton count={3} /> : failure === "offline" ? <CatalogState icon="refresh" onRetry={refresh} subtitle={t("ui.offlineSubtitle")} title={t("ui.offlineTitle")} /> : failure === "server" ? <CatalogState icon="refresh" onRetry={refresh} subtitle={t("search.loadFailedSubtitle")} title={t("search.loadFailedTitle")} /> : bloggers.length ? bloggers.map((blogger) => <BloggerCard blogger={blogger} key={blogger.id} />) : <CatalogState icon="search" subtitle={t("search.emptySubtitle")} title={t("search.emptyTitle")} />}
      {!loading && !failure && bloggers.length > 0 && <>
        <div aria-hidden="true" ref={sentinelRef} />
        {loadingMore && <SearchSkeleton compact count={2} />}
        {loadMoreFailed && <CatalogState compact icon="refresh" onRetry={() => void load(page + 1, true)} subtitle={t("search.loadFailedSubtitle")} title={t("search.loadFailedTitle")} />}
        {loadedInitialResult && !hasMore && !loadingMore && !loadMoreFailed && <p className="catalog-search__end">{t("search.endOfList")}</p>}
      </>}
    </section>
    <BottomNav />
  </div>;
}

function buildActiveChips(filters: BloggerSearchFilters, t: Translate) {
  const chips: Array<{ key: FilterKey; label: string }> = [];
  if (filters.category) chips.push({ key: "category", label: safeCategoryLabel(filters.category, t) });
  if (filters.city) chips.push({ key: "city", label: cityLabel(filters.city) });
  if (filters.platform) chips.push({ key: "platform", label: platformOptions(t).find(([value]) => value === filters.platform)?.[1] ?? t("common.notSpecified") });
  if (filters.minFollowers) chips.push({ key: "minFollowers", label: t("search.followersAtLeast", { count: formatNumber(filters.minFollowers) }) });
  if (filters.minEr) chips.push({ key: "minEr", label: t("search.erAtLeast", { value: formatPercentage(filters.minEr) }) });
  if (filters.maxPrice) chips.push({ key: "maxPrice", label: t("search.priceUpTo", { amount: formatCurrency(filters.maxPrice) }) });
  if (filters.sort && filters.sort !== defaultFilters.sort) chips.push({ key: "sort", label: sortOptions(t).find(([value]) => value === filters.sort)?.[1] ?? t("search.sortPopular") });
  return chips;
}

function ActiveFilterChips({ chips, onRemove, onReset }: { chips: Array<{ key: FilterKey; label: string }>; onRemove: (key: FilterKey) => void; onReset: () => void }) {
  const { t } = useI18n();
  if (!chips.length) return null;
  return <div aria-label={t("search.activeFilters")} className="catalog-search__chips" role="group">{chips.map((chip) => <button aria-label={t("search.removeFilterAria", { filter: chip.label })} className="catalog-search__chip" key={chip.key} onClick={() => onRemove(chip.key)} type="button"><span>{chip.label}</span><Icon aria-hidden="true" className="h-3.5 w-3.5" name="close" /></button>)}<button className="catalog-search__reset-all" onClick={onReset} type="button">{t("search.resetAll")}</button></div>;
}

function FilterSelect({ label, value, options, onChange }: { label: string; value: string; options: string[][]; onChange: (value: string) => void }) {
  const { haptic } = useTelegram();
  return <label className="catalog-search__filter-select"><span>{label}</span><select onChange={(event) => { haptic.selection(); onChange(event.target.value); }} value={value}>{options.map(([optionValue, optionLabel]) => <option key={optionValue} value={optionValue}>{optionLabel}</option>)}</select></label>;
}

function SearchSkeleton({ count, compact = false }: { count: number; compact?: boolean }) {
  return <div aria-hidden="true" className={compact ? "catalog-search__next-skeletons" : "catalog-search__skeletons"}>{Array.from({ length: count }, (_, index) => <Skeleton className={compact ? "catalog-search__skeleton catalog-search__skeleton--compact" : "catalog-search__skeleton"} key={index} />)}</div>;
}

function CatalogState({ title, subtitle, icon, onRetry, compact = false }: { title: string; subtitle: string; icon: string; onRetry?: () => void; compact?: boolean }) {
  const { t } = useI18n();
  return <div className={`catalog-search__state${compact ? " catalog-search__state--compact" : ""}`} role="status"><span aria-hidden="true" className="catalog-search__state-icon"><Icon name={icon} /></span><h2>{title}</h2><p>{subtitle}</p>{onRetry && <button className="catalog-search__primary-button" onClick={onRetry} type="button">{t("common.retry")}</button>}</div>;
}

function safeCategoryLabel(value: string, t: Translate) {
  const label = categoryLabel(value);
  return label.startsWith("taxonomy.category.") ? t("common.notSpecified") : label;
}

function platformOptions(t: Translate): string[][] {
  return [["", t("common.all")], ["instagram", t("search.platformInstagram")], ["telegram", t("search.platformTelegram")], ["tiktok", t("search.platformTiktok")], ["youtube", t("search.platformYoutube")]];
}

function followersOptions(t: Translate): string[][] {
  return [["", t("common.all")], ...[10_000, 50_000, 100_000].map((value) => [String(value), t("search.followersAtLeast", { count: formatNumber(value) })])];
}

function erOptions(t: Translate): string[][] {
  return [["", t("common.all")], ...[3, 5, 8].map((value) => [String(value), t("search.erAtLeast", { value: formatPercentage(value) })])];
}

function priceOptions(t: Translate): string[][] {
  return [["", t("search.anyPrice")], ...[300_000, 500_000, 1_000_000].map((value) => [String(value), t("search.priceUpTo", { amount: formatCurrency(value) })])];
}

function sortOptions(t: Translate): string[][] {
  return [["popular", t("search.sortPopular")], ["rating", t("search.sortRating")], ["er", t("search.sortEr")], ["price", t("search.sortPrice")], ["newest", t("search.sortNewest")]];
}
