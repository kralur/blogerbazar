import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { getBrandFaceCatalog, getCategories, type BrandFaceCatalogFilters, type BrandFaceCatalogSort, type BrandFaceCatalogItem } from "../../api/marketplace";
import { BrandFaceCard } from "../BrandFaceCard";
import { LanguageSwitcher } from "../LanguageSwitcher";
import { useDebouncedValue } from "../../hooks/useDebouncedValue";
import { useProfileDataRefresh } from "../../hooks/useProfileDataRefresh";
import { useScrollRestoration } from "../../hooks/useScrollRestoration";
import { categoryLabel, cityLabel, useI18n } from "../../i18n";
import { formatCurrency } from "../../lib/currency";
import { uzbekistanRegions } from "../../lib/taxonomy";
import { useTelegram } from "../../telegram/TelegramProvider";
import { BottomSheet, Icon, SearchBar } from "../ui";
import { CatalogHeader, CatalogState, FilterSelect, SearchSkeleton, ActiveFilterChips } from "./CatalogShared";
import { CatalogTypeSegmentedControl } from "./CatalogTypeSegmentedControl";
import { usePaginatedCatalog } from "./usePaginatedCatalog";

const pageSize = 20;
const filterSheetId = "brand-face-search-filters";
const defaultFilters: BrandFaceCatalogFilters = { sort: "promoted", pageSize };

type FilterKey = "category" | "city" | "language" | "minPrice" | "maxPrice" | "sort";
type Translate = (key: string, values?: Record<string, string | number>) => string;

function hashCategory() {
  const hash = window.location.hash;
  if (!hash.startsWith("#/search")) return null;
  return new URLSearchParams(hash.split("?")[1] ?? "").get("category");
}

function normalizedFilters(filters: BrandFaceCatalogFilters): BrandFaceCatalogFilters {
  return {
    ...defaultFilters,
    ...Object.fromEntries(Object.entries(filters).filter(([, value]) => value !== "" && value !== undefined)),
    pageSize
  };
}

function filtersEqual(left: BrandFaceCatalogFilters, right: BrandFaceCatalogFilters) {
  const keys = new Set([...Object.keys(left), ...Object.keys(right)]);
  return [...keys].every((key) => left[key as keyof BrandFaceCatalogFilters] === right[key as keyof BrandFaceCatalogFilters]);
}

export function BrandFaceCatalog({ active, onSelectType }: { active: boolean; onSelectType: (type: "blogger" | "brand-face") => void }) {
  const { t } = useI18n();
  const { haptic } = useTelegram();
  useScrollRestoration("search:brand-face", active);
  const initialCategory = hashCategory() ?? "";
  const [categories, setCategories] = useState<string[]>([]);
  const [filtersOpen, setFiltersOpen] = useState(false);
  const [query, setQuery] = useState("");
  const [appliedFilters, setAppliedFilters] = useState<BrandFaceCatalogFilters>(() => normalizedFilters({ category: initialCategory }));
  const [draftFilters, setDraftFilters] = useState<BrandFaceCatalogFilters>(() => normalizedFilters({ category: initialCategory }));
  const draftFiltersRef = useRef<BrandFaceCatalogFilters>(normalizedFilters({ category: initialCategory }));
  const sentinelRef = useRef<HTMLDivElement | null>(null);
  const debouncedQuery = useDebouncedValue(query, 300);

  const fetchPage = useCallback((requestedPage: number, signal: AbortSignal) => getBrandFaceCatalog({ ...appliedFilters, query: debouncedQuery || undefined, page: requestedPage, pageSize }, signal), [appliedFilters, debouncedQuery]);
  const { items, total, loading, loadingMore, loadMoreFailed, failure, page, hasMore, loadedInitialResult, load, cancel } = usePaginatedCatalog<BrandFaceCatalogItem>({ active, fetchPage });

  const refresh = useCallback(() => { void load(1, false); }, [load]);

  useEffect(() => {
    void getCategories().then(setCategories).catch(() => undefined);
  }, []);

  useEffect(() => {
    if (!active) {
      setFiltersOpen(false);
      cancel();
      return;
    }
    void load(1, false);
    return () => {
      cancel();
    };
  }, [active, cancel, load]);

  useProfileDataRefresh(refresh);

  useEffect(() => {
    const syncHashCategory = () => {
      if (!active) return;
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
    syncHashCategory();
    window.addEventListener("hashchange", syncHashCategory);
    return () => window.removeEventListener("hashchange", syncHashCategory);
  }, [active]);

  useEffect(() => {
    if (!active) return;
    const sentinel = sentinelRef.current;
    if (!sentinel || loading || loadingMore || loadMoreFailed || !hasMore || failure) return;
    const observer = new IntersectionObserver(([entry]) => {
      if (entry.isIntersecting) void load(page + 1, true);
    }, { rootMargin: "240px" });
    observer.observe(sentinel);
    return () => observer.disconnect();
  }, [active, failure, hasMore, load, loadMoreFailed, loading, loadingMore, page]);

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

  const activeChips = useMemo(() => buildActiveChips(appliedFilters, t), [appliedFilters, t]);

  return <div aria-hidden={!active} className="catalog-search screen screen--with-nav" hidden={!active}>
    <CatalogHeader>
      <div className="catalog-search__heading"><p className="catalog-search__eyebrow">{t("search.eyebrow")}</p><h1>{t("search.title")}</h1></div>
      <LanguageSwitcher />
    </CatalogHeader>
    <CatalogTypeSegmentedControl onChange={onSelectType} value="brand-face" />
    <div className="catalog-search__searchbar"><SearchBar className="catalog-search__search-control" onChange={(event) => setQuery(event.target.value)} placeholder={t("search.brandFacePlaceholder")} value={query} /></div>
    <div className="catalog-search__controls"><button aria-controls={filterSheetId} aria-expanded={filtersOpen} aria-label={t("search.filters")} className="catalog-search__filter-button" onClick={openFilters} type="button"><Icon name="filter" /><span>{t("search.filters")}</span></button></div>
    <ActiveFilterChips chips={activeChips} onRemove={removeFilter} onReset={resetFilters} />
    <BottomSheet id={filterSheetId} onClose={() => setFiltersOpen(false)} open={filtersOpen} title={t("search.filters")}><div className="catalog-search__sheet-content">
      <FilterSelect label={t("common.categories")} onChange={(value) => setDraft("category", value)} options={[["", t("common.all")], ...categories.map((category) => [category, safeCategoryLabel(category, t)])]} value={draftFilters.category ?? ""} />
      <FilterSelect label={t("common.city")} onChange={(value) => setDraft("city", value)} options={[["", t("common.any")], ...uzbekistanRegions.map((city) => [city, cityLabel(city)])]} value={draftFilters.city ?? ""} />
      <label className="catalog-search__filter-select"><span>{t("search.language")}</span><input aria-label={t("search.language")} onChange={(event) => setDraft("language", event.target.value)} placeholder={t("search.languagePlaceholder")} type="text" value={draftFilters.language ?? ""} /></label>
      <label className="catalog-search__filter-select"><span>{t("search.minPrice")}</span><input aria-label={t("search.minPrice")} inputMode="numeric" onChange={(event) => setDraft("minPrice", Number(event.target.value) || undefined)} placeholder={t("search.anyPrice")} type="number" value={draftFilters.minPrice ?? ""} /></label>
      <label className="catalog-search__filter-select"><span>{t("search.maxPrice")}</span><input aria-label={t("search.maxPrice")} inputMode="numeric" onChange={(event) => setDraft("maxPrice", Number(event.target.value) || undefined)} placeholder={t("search.anyPrice")} type="number" value={draftFilters.maxPrice ?? ""} /></label>
      <FilterSelect label={t("search.sort")} onChange={(value) => setDraft("sort", value as BrandFaceCatalogSort)} options={sortOptions(t)} value={draftFilters.sort ?? "promoted"} />
      <div className="catalog-search__sheet-actions"><button className="catalog-search__secondary-button" onClick={resetFilters} type="button">{t("common.reset")}</button><button className="catalog-search__primary-button" onClick={applyFilters} type="button">{t("common.apply")}</button></div>
    </div></BottomSheet>
    <p aria-live="polite" className="catalog-search__results-count">{loading ? t("search.brandFacesLoading") : t("search.found", { count: total })}</p>
    <section aria-busy={loading || loadingMore} aria-live="polite" className="catalog-search__results">
      {loading ? <SearchSkeleton count={3} /> : failure === "offline" ? <CatalogState icon="refresh" onRetry={refresh} subtitle={t("ui.offlineSubtitle")} title={t("ui.offlineTitle")} /> : failure === "server" ? <CatalogState icon="refresh" onRetry={refresh} subtitle={t("search.brandFaceLoadFailedSubtitle")} title={t("search.brandFaceLoadFailedTitle")} /> : items.length ? items.map((profile) => <BrandFaceCard key={profile.id} profile={profile} />) : <CatalogState icon="search" subtitle={t("search.brandFaceEmptySubtitle")} title={t("search.brandFaceEmptyTitle")} />}
      {!loading && !failure && items.length > 0 && <>
        <div aria-hidden="true" ref={sentinelRef} />
        {loadingMore && <SearchSkeleton compact count={2} />}
        {loadMoreFailed && <CatalogState compact icon="refresh" onRetry={() => void load(page + 1, true)} subtitle={t("search.brandFaceLoadFailedSubtitle")} title={t("search.brandFaceLoadFailedTitle")} />}
        {loadedInitialResult && !hasMore && !loadingMore && !loadMoreFailed && <p className="catalog-search__end">{t("search.brandFaceEndOfList")}</p>}
      </>}
    </section>
  </div>;
}

function buildActiveChips(filters: BrandFaceCatalogFilters, t: Translate) {
  const chips: Array<{ key: FilterKey; label: string }> = [];
  if (filters.category) chips.push({ key: "category", label: safeCategoryLabel(filters.category, t) });
  if (filters.city) chips.push({ key: "city", label: cityLabel(filters.city) });
  if (filters.language) chips.push({ key: "language", label: filters.language });
  if (filters.minPrice) chips.push({ key: "minPrice", label: t("search.priceFrom", { amount: formatCurrency(filters.minPrice) }) });
  if (filters.maxPrice) chips.push({ key: "maxPrice", label: t("search.priceUpTo", { amount: formatCurrency(filters.maxPrice) }) });
  if (filters.sort && filters.sort !== defaultFilters.sort) chips.push({ key: "sort", label: sortOptions(t).find(([value]) => value === filters.sort)?.[1] ?? t("search.sortPromoted") });
  return chips;
}

function safeCategoryLabel(value: string, t: Translate) {
  const label = categoryLabel(value);
  return label.startsWith("taxonomy.category.") ? t("common.notSpecified") : label;
}

function sortOptions(t: Translate): string[][] {
  return [["promoted", t("search.sortPromoted")], ["newest", t("search.sortNewest")], ["price_asc", t("search.sortPriceAsc")], ["price_desc", t("search.sortPriceDesc")]];
}
