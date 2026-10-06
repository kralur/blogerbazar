import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { formatShortDate } from "../lib/currency";
import { getMyCampaignApplicationsPage, type MarketplaceRole, type MyCampaignApplicationItem } from "../api/marketplace";
import { CatalogState, FilterSelect, SearchSkeleton } from "../components/catalog/CatalogShared";
import { usePaginatedCatalog } from "../components/catalog/usePaginatedCatalog";
import { Badge } from "../components/ui";
import { useI18n } from "../i18n";
import { campaignApplicationStatusLabelKey, campaignApplicationStatusTone, type CampaignApplicationStatus } from "../lib/campaignApplicationStatus";
import { subscribeCampaignApplicationCache } from "../data/campaignApplicationCache";
import { useRootScreenVisibility } from "../navigation/RootScreenVisibility";
import { RequestRow } from "../components/RequestRow";

const pageSize = 20;

export function BloggerApplications({ activeMarketplaceRole }: { activeMarketplaceRole?: MarketplaceRole }) {
  const { language, t } = useI18n();
  const visible = useRootScreenVisibility();
  const [status, setStatus] = useState<CampaignApplicationStatus | undefined>();
  const sentinelRef = useRef<HTMLDivElement | null>(null);
  const request = useMemo(() => ({ status, pageSize }), [status]);
  const fetchPage = useCallback((page: number, signal: AbortSignal) => getMyCampaignApplicationsPage({ ...request, page }, signal), [request]);
  const allowed = activeMarketplaceRole === "Blogger";
  const catalog = usePaginatedCatalog<MyCampaignApplicationItem>({ active: visible && allowed, fetchPage });

  useEffect(() => {
    if (visible && allowed) void catalog.load(1, false);
  }, [allowed, visible, request, catalog.load]);

  useEffect(() => {
    if (!visible) {
      catalog.cancel();
      return;
    }
    return () => catalog.cancel();
  }, [catalog.cancel, visible]);

  useEffect(() => subscribeCampaignApplicationCache(() => {
    if (visible && allowed) void catalog.load(1, false, true);
  }), [allowed, catalog.load, visible]);

  useEffect(() => {
    const sentinel = sentinelRef.current;
    if (!sentinel || !catalog.hasMore || catalog.loading || catalog.loadingMore || catalog.loadMoreFailed || catalog.failure) return;
    const observer = new IntersectionObserver(([entry]) => { if (entry.isIntersecting) void catalog.load(catalog.page + 1, true); }, { rootMargin: "240px" });
    observer.observe(sentinel);
    return () => observer.disconnect();
  }, [catalog.failure, catalog.hasMore, catalog.load, catalog.loadMoreFailed, catalog.loading, catalog.loadingMore, catalog.page]);

  const retry = () => void catalog.load(1, false, catalog.loadedInitialResult);
  if (!allowed) return <CatalogState icon="lock" subtitle={t("applications.deniedSubtitle")} title={t("applications.deniedTitle")} />;
  return <section aria-busy={catalog.loading || catalog.loadingMore} className="catalog-search__results">
    <div className="my-campaigns__controls"><FilterSelect label={t("applications.status")} onChange={(value) => setStatus(value === "" ? undefined : Number(value) as CampaignApplicationStatus)} options={statusOptions(t)} value={status == null ? "" : String(status)} />{status != null && <button className="catalog-search__reset-all" onClick={() => setStatus(undefined)} type="button">{t("common.reset")}</button>}</div>
    {!(catalog.failure && !catalog.loadedInitialResult) && <p aria-live="polite" className="catalog-search__results-count">{catalog.loading && !catalog.loadedInitialResult ? t("applications.loading") : t("search.found", { count: catalog.total })}</p>}
    {catalog.loading && !catalog.loadedInitialResult && <SearchSkeleton count={3} compact />}
    {catalog.failure && !catalog.loadedInitialResult && <CatalogState icon="refresh" onRetry={retry} subtitle={t("applications.errorSubtitle")} title={t("applications.errorTitle")} />}
    {catalog.loadedInitialResult && catalog.failure && <CatalogState compact icon="refresh" onRetry={retry} subtitle={t("applications.errorSubtitle")} title={t("applications.errorTitle")} />}
    {!catalog.loading && !catalog.failure && catalog.items.length === 0 && <CatalogState icon={status == null ? "briefcase" : "filter"} onRetry={status == null ? undefined : () => setStatus(undefined)} subtitle={t(status == null ? "applications.emptySubtitle" : "applications.filteredEmptySubtitle")} title={t(status == null ? "applications.emptyTitle" : "applications.filteredEmptyTitle")} />}
    <div className="request-list">{catalog.items.map((item) => <RequestRow ariaLabel={t("applications.openAria", { title: item.campaignTitle })} href={`#/my-application/${item.id}`} imageUrl={item.businessAvatarUrl} key={item.id} meta={t("applications.sentAt", { date: formatShortDate(item.createdAtUtc, language) })} name={item.businessName} status={<Badge tone={campaignApplicationStatusTone(item.status)}>{t(campaignApplicationStatusLabelKey(item.status))}</Badge>} title={item.campaignTitle} />)}</div>
    {catalog.hasMore && <div aria-hidden="true" ref={sentinelRef} />}
    {catalog.loadingMore && <SearchSkeleton compact count={2} />}
    {catalog.loadMoreFailed && <CatalogState compact icon="refresh" onRetry={() => void catalog.load(catalog.page + 1, true)} subtitle={t("applications.errorSubtitle")} title={t("applications.errorTitle")} />}
    {catalog.loadedInitialResult && !catalog.hasMore && catalog.items.length > 0 && <p className="catalog-search__end">{t("applications.end")}</p>}
  </section>;
}

function statusOptions(t: (key: string) => string): string[][] {
  return [["", t("applications.status.all")], ...([0, 1, 2, 3, 4] as CampaignApplicationStatus[]).map((status) => [String(status), t(campaignApplicationStatusLabelKey(status))])];
}
