import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { getCurrentPlatformUser, getMyCampaigns, normalizeMarketplaceRole, type MyCampaign, type MyCampaignQuery, type MyCampaignSort, type MyCampaignStatus } from "../api/marketplace";
import { MyCampaignCard } from "../components/MyCampaignCard";
import { ManagementBackLink } from "../components/ManagementBackLink";
import { CatalogHeader, CatalogState, FilterSelect, SearchSkeleton } from "../components/catalog/CatalogShared";
import { usePaginatedCatalog } from "../components/catalog/usePaginatedCatalog";
import { LanguageSwitcher } from "../components/LanguageSwitcher";
import { BottomNav, SearchBar } from "../components/ui";
import { useDebouncedValue } from "../hooks/useDebouncedValue";
import { useCampaignDataRefresh } from "../hooks/useCampaignDataRefresh";
import { useScrollRestoration } from "../hooks/useScrollRestoration";
import { useI18n } from "../i18n";
import { campaignStatusLabel } from "../lib/campaignStatus";
import { useRootScreenVisibility } from "../navigation/RootScreenVisibility";

const pageSize = 20;
const defaultQuery: MyCampaignQuery = { sort: "newest", pageSize };

export function MyCampaigns() {
  const { t } = useI18n();
  const active = useRootScreenVisibility();
  useScrollRestoration("my-campaigns");
  const [query, setQuery] = useState("");
  const [status, setStatus] = useState<MyCampaignStatus | undefined>();
  const [sort, setSort] = useState<MyCampaignSort>("newest");
  const [access, setAccess] = useState<"checking" | "allowed" | "denied" | "failed">("checking");
  const debouncedQuery = useDebouncedValue(query, 300);
  const lastCatalogKeyRef = useRef("");
  const sentinelRef = useRef<HTMLDivElement | null>(null);

  const request = useMemo<MyCampaignQuery>(() => ({
    ...defaultQuery,
    query: debouncedQuery.trim() || undefined,
    status,
    sort
  }), [debouncedQuery, sort, status]);
  const catalogKey = useMemo(() => JSON.stringify(request), [request]);
  const fetchPage = useCallback((requestedPage: number, signal: AbortSignal) => getMyCampaigns({ ...request, page: requestedPage }, signal), [request]);
  const { items, total, loading, loadingMore, loadMoreFailed, failure, page, hasMore, loadedInitialResult, load, cancel } = usePaginatedCatalog<MyCampaign>({ active: active && access === "allowed", fetchPage });
  const hasFilters = Boolean(debouncedQuery.trim()) || status !== undefined || sort !== "newest";

  const refreshAccess = useCallback(() => {
    if (!active) return;
    let cancelled = false;
    setAccess("checking");
    getCurrentPlatformUser().then((user) => {
      if (!cancelled) setAccess(normalizeMarketplaceRole(user.selectedMarketplaceRole) === "Business" ? "allowed" : "denied");
    }).catch(() => {
      if (!cancelled) setAccess("failed");
    });
    return () => { cancelled = true; };
  }, [active]);

  useEffect(() => refreshAccess(), [refreshAccess]);

  useEffect(() => {
    if (!active || access !== "allowed" || loading || (lastCatalogKeyRef.current === catalogKey && (loadedInitialResult || failure))) return;
    lastCatalogKeyRef.current = catalogKey;
    void load(1, false);
  }, [access, active, catalogKey, failure, load, loadedInitialResult, loading]);

  useEffect(() => {
    if (!active || access !== "allowed") {
      cancel();
      return;
    }
    const sentinel = sentinelRef.current;
    if (!sentinel || loading || loadingMore || loadMoreFailed || !hasMore || failure) return;
    const observer = new IntersectionObserver(([entry]) => {
      if (entry.isIntersecting) void load(page + 1, true);
    }, { rootMargin: "240px" });
    observer.observe(sentinel);
    return () => observer.disconnect();
  }, [access, active, cancel, failure, hasMore, load, loadMoreFailed, loading, loadingMore, page]);

  const reset = () => {
    setQuery("");
    setStatus(undefined);
    setSort("newest");
  };
  const retry = () => void load(1, false);
  const retryMore = () => void load(page + 1, true);
  const refreshCampaigns = useCallback(() => {
    if (active && access === "allowed") {
      lastCatalogKeyRef.current = "";
      void load(1, false);
    }
  }, [access, active, load]);
  useCampaignDataRefresh(refreshCampaigns, active && access === "allowed");

  return <div aria-hidden={!active} className="campaign-management-screen my-campaigns catalog-search screen screen--with-nav" hidden={!active}>
    <CatalogHeader className="my-campaigns__header">
      <div className="my-campaigns__heading-row"><ManagementBackLink ariaLabel={t("myCampaigns.backAria")} href="#/profile" /><div className="catalog-search__heading"><p className="catalog-search__eyebrow">{t("myCampaigns.eyebrow")}</p><h1>{t("myCampaigns.title")}</h1></div></div>
      <div className="my-campaigns__header-actions">{access === "allowed" && <a className="my-campaigns__create" href="#/campaigns">{t("myCampaigns.create")}</a>}<LanguageSwitcher /></div>
    </CatalogHeader>
    {access === "checking" ? <SearchSkeleton count={3} /> : access === "denied" ? <CatalogState icon="lock" subtitle={t("myCampaigns.deniedSubtitle")} title={t("myCampaigns.deniedTitle")} /> : access === "failed" ? <CatalogState icon="refresh" onRetry={refreshAccess} subtitle={t("myCampaigns.accessErrorSubtitle")} title={t("myCampaigns.accessErrorTitle")} /> : <>
    <div className="catalog-search__searchbar"><SearchBar clearAriaLabel={t("myCampaigns.clearSearchAria")} className="catalog-search__search-control" onChange={(event) => setQuery(event.target.value)} onClear={() => setQuery("")} placeholder={t("myCampaigns.searchPlaceholder")} value={query} /></div>
    <div className="my-campaigns__controls">
      <FilterSelect label={t("myCampaigns.statusFilter")} onChange={(value) => setStatus(value === "" ? undefined : Number(value) as MyCampaignStatus)} options={[["", t("common.all")], ...([0, 1, 2, 3] as MyCampaignStatus[]).map((value) => [String(value), campaignStatusLabel(value, t)])]} value={status == null ? "" : String(status)} />
      <FilterSelect label={t("search.sort")} onChange={(value) => setSort(value as MyCampaignSort)} options={sortOptions(t)} value={sort} />
      {hasFilters && <button className="catalog-search__reset-all" onClick={reset} type="button">{t("myCampaigns.clearFilters")}</button>}
    </div>
    <p aria-live="polite" className="catalog-search__results-count">{loading ? t("myCampaigns.loading") : t("myCampaigns.found", { count: total })}</p>
    <section aria-busy={loading || loadingMore} aria-live="polite" className="catalog-search__results">
      {loading && <SearchSkeleton count={3} />}
      {!loading && failure && <CatalogState icon={failure === "offline" ? "refresh" : "filter"} onRetry={retry} subtitle={t(failure === "offline" ? "myCampaigns.offlineSubtitle" : "myCampaigns.errorSubtitle")} title={t(failure === "offline" ? "myCampaigns.offlineTitle" : "myCampaigns.errorTitle")} />}
      {!loading && !failure && items.length === 0 && <CatalogState actionLabel={hasFilters ? t("myCampaigns.clearFilters") : t("myCampaigns.create")} icon={hasFilters ? "filter" : "briefcase"} onRetry={hasFilters ? reset : () => { window.location.hash = "/campaigns"; }} subtitle={t(hasFilters ? "myCampaigns.filteredEmptySubtitle" : "myCampaigns.emptySubtitle")} title={t(hasFilters ? "myCampaigns.filteredEmptyTitle" : "myCampaigns.emptyTitle")} />}
      {!loading && !failure && items.map((campaign) => <MyCampaignCard campaign={campaign} key={campaign.id} />)}
      {hasMore && <div aria-hidden="true" ref={sentinelRef} />}
      {loadingMore && <SearchSkeleton compact count={2} />}
      {loadMoreFailed && <CatalogState compact icon="refresh" onRetry={retryMore} subtitle={t("myCampaigns.loadMoreSubtitle")} title={t("myCampaigns.loadMoreTitle")} />}
      {loadedInitialResult && !hasMore && !loadingMore && !loadMoreFailed && items.length > 0 && <p className="catalog-search__end">{t("myCampaigns.endOfList")}</p>}
    </section>
    </>}
    <BottomNav />
  </div>;
}

function sortOptions(t: (key: string) => string): string[][] {
  return [
    ["newest", t("myCampaigns.sortNewest")],
    ["oldest", t("myCampaigns.sortOldest")],
    ["deadline_asc", t("myCampaigns.sortDeadlineAsc")],
    ["deadline_desc", t("myCampaigns.sortDeadlineDesc")],
    ["budget_asc", t("myCampaigns.sortBudgetAsc")],
    ["budget_desc", t("myCampaigns.sortBudgetDesc")]
  ];
}
