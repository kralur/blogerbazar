import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { ApiError, getApiErrorMessage } from "../api/client";
import { acceptMyCampaignApplication, getCampaignApplicationInbox, getCurrentPlatformUser, normalizeMarketplaceRole, rejectMyCampaignApplication, type CampaignApplicationInboxItem } from "../api/marketplace";
import { CatalogState, FilterSelect, SearchSkeleton } from "../components/catalog/CatalogShared";
import { usePaginatedCatalog } from "../components/catalog/usePaginatedCatalog";
import { Avatar, Badge, BottomNav, Button, Card, Modal, Toast } from "../components/ui";
import { useI18n } from "../i18n";
import { campaignApplicationStatusLabelKey, campaignApplicationStatusTone, canAcceptCampaignApplication, type CampaignApplicationStatus } from "../lib/campaignApplicationStatus";
import { ManagementBackLink } from "../components/ManagementBackLink";

const pageSize = 20;

export function MyCampaignApplications({ campaignId }: { campaignId: string }) {
  const { language, t } = useI18n();
  const [access, setAccess] = useState<"checking" | "allowed" | "denied" | "failed">("checking");
  const [status, setStatus] = useState<CampaignApplicationStatus | undefined>();
  const [decision, setDecision] = useState<{ item: CampaignApplicationInboxItem; value: "accept" | "reject" } | null>(null);
  const [deciding, setDeciding] = useState(false);
  const [toast, setToast] = useState("");
  const [toastTone, setToastTone] = useState<"success" | "error">("success");
  const [reload, setReload] = useState(0);
  const [resourceFailure, setResourceFailure] = useState<"denied" | "not-found" | null>(null);
  const sentinelRef = useRef<HTMLDivElement | null>(null);
  const campaignIdRef = useRef(campaignId);
  campaignIdRef.current = campaignId;
  const decisionRef = useRef(0);
  const decidingRef = useRef(false);
  const request = useMemo(() => ({ status, pageSize }), [status]);
  const fetchPage = useCallback(async (page: number, signal: AbortSignal) => {
    try {
      const result = await getCampaignApplicationInbox(campaignId, { ...request, page }, signal);
      setResourceFailure(null);
      return result;
    } catch (error) {
      if (error instanceof ApiError && (error.status === 401 || error.status === 403)) setResourceFailure("denied");
      if (error instanceof ApiError && error.status === 404) setResourceFailure("not-found");
      throw error;
    }
  }, [campaignId, request]);
  const catalog = usePaginatedCatalog<CampaignApplicationInboxItem>({ active: access === "allowed", fetchPage });

  useEffect(() => {
    let cancelled = false;
    getCurrentPlatformUser().then((user) => { if (!cancelled) setAccess(normalizeMarketplaceRole(user.selectedMarketplaceRole) === "Business" ? "allowed" : "denied"); }).catch(() => { if (!cancelled) setAccess("failed"); });
    return () => { cancelled = true; };
  }, [reload]);
  useEffect(() => { if (access === "allowed") void catalog.load(1, false); }, [access, catalog.load, request]);
  useEffect(() => () => { decisionRef.current += 1; decidingRef.current = false; catalog.cancel(); }, [catalog.cancel, campaignId]);
  useEffect(() => {
    const sentinel = sentinelRef.current;
    if (!sentinel || !catalog.hasMore || catalog.loading || catalog.loadingMore || catalog.loadMoreFailed || catalog.failure) return;
    const observer = new IntersectionObserver(([entry]) => { if (entry.isIntersecting) void catalog.load(catalog.page + 1, true); }, { rootMargin: "240px" });
    observer.observe(sentinel);
    return () => observer.disconnect();
  }, [catalog.failure, catalog.hasMore, catalog.load, catalog.loadMoreFailed, catalog.loading, catalog.loadingMore, catalog.page]);
  const decide = async () => {
    if (!decision || decidingRef.current) return;
    decidingRef.current = true;
    const decisionId = ++decisionRef.current;
    const decisionCampaignId = campaignId;
    const applicationId = decision.item.id;
    setDeciding(true);
    try {
      const result = decision.value === "accept" ? await acceptMyCampaignApplication(decisionCampaignId, applicationId) : await rejectMyCampaignApplication(decisionCampaignId, applicationId);
      if (decisionId !== decisionRef.current || campaignIdRef.current !== decisionCampaignId) return;
      catalog.updateItem(result.id, (item) => ({ ...item, status: result.status }));
      setDecision(null); setToastTone("success"); setToast(t(result.status === 2 ? "applications.status.accepted" : "applications.status.rejected"));
    } catch (error) {
      if (decisionId !== decisionRef.current || campaignIdRef.current !== decisionCampaignId) return;
      if (error instanceof ApiError && error.status === 409) {
        setDecision(null);
        await catalog.load(1, false, true);
      } else if (error instanceof ApiError && error.status === 404) {
        setDecision(null); setResourceFailure("not-found");
      } else if (error instanceof ApiError && (error.status === 401 || error.status === 403)) {
        setDecision(null); setResourceFailure("denied");
      } else {
        setToastTone("error"); setToast(getApiErrorMessage(error, t("applications.decisionFailed")));
      }
    } finally {
      if (decisionId === decisionRef.current && campaignIdRef.current === decisionCampaignId) {
        decidingRef.current = false;
        setDeciding(false);
      }
    }
  };
  const locale = language === "uz" ? "uz-UZ" : "ru-RU";
  const retry = () => void catalog.load(1, false, catalog.loadedInitialResult);
  if (access === "checking") return <div className="screen screen--with-nav"><SearchSkeleton count={3} compact /><BottomNav /></div>;
  if (access === "denied") return <div className="screen screen--with-nav"><CatalogState icon="lock" subtitle={t("applications.deniedSubtitle")} title={t("applications.deniedTitle")} /><BottomNav /></div>;
  if (access === "failed") return <div className="screen screen--with-nav"><CatalogState icon="refresh" onRetry={() => setReload((value) => value + 1)} subtitle={t("applications.errorSubtitle")} title={t("applications.errorTitle")} /><BottomNav /></div>;
  if (resourceFailure) return <div className="screen screen--with-nav"><CatalogState icon={resourceFailure === "denied" ? "lock" : "filter"} subtitle={t(resourceFailure === "denied" ? "applications.deniedSubtitle" : "applications.notFoundSubtitle")} title={t(resourceFailure === "denied" ? "applications.deniedTitle" : "applications.notFoundTitle")} /><BottomNav /></div>;
  return <div className="screen screen--with-nav catalog-search"><header className="flex items-center justify-between"><ManagementBackLink ariaLabel={t("nav.campaigns")} href={`#/my-campaign/${campaignId}`} /><h1 className="text-xl font-extrabold">{t("applications.inboxTitle")}</h1></header><div className="my-campaigns__controls"><FilterSelect label={t("applications.status")} onChange={(value) => setStatus(value === "" ? undefined : Number(value) as CampaignApplicationStatus)} options={statusOptions(t)} value={status == null ? "" : String(status)} />{status != null && <button className="catalog-search__reset-all" onClick={() => setStatus(undefined)} type="button">{t("common.reset")}</button>}</div><section aria-busy={catalog.loading || catalog.loadingMore} className="catalog-search__results">{catalog.loading && !catalog.loadedInitialResult && <SearchSkeleton count={3} compact />}{catalog.failure && !catalog.loadedInitialResult && <CatalogState icon="refresh" onRetry={retry} subtitle={t("applications.errorSubtitle")} title={t("applications.errorTitle")} />}{catalog.loadedInitialResult && catalog.failure && <CatalogState compact icon="refresh" onRetry={retry} subtitle={t("applications.errorSubtitle")} title={t("applications.errorTitle")} />}{!catalog.loading && !catalog.failure && catalog.items.length === 0 && <CatalogState icon={status == null ? "briefcase" : "filter"} onRetry={status == null ? undefined : () => setStatus(undefined)} subtitle={t(status == null ? "applications.inboxEmptySubtitle" : "applications.filteredEmptySubtitle")} title={t(status == null ? "applications.inboxEmptyTitle" : "applications.filteredEmptyTitle")} />}{catalog.items.map((item) => <Card className="application-inbox-card mt-3" key={item.id}><div className="flex gap-3"><Avatar name={item.bloggerName} size="sm" src={item.bloggerAvatarUrl} /><div className="min-w-0 flex-1"><div className="flex items-start justify-between gap-2"><div className="min-w-0"><h2 className="truncate font-extrabold">{item.bloggerName}</h2><p className="mt-1 truncate text-sm text-brand-muted">{item.city}</p></div><Badge tone={campaignApplicationStatusTone(item.status)}>{t(campaignApplicationStatusLabelKey(item.status))}</Badge></div><p className="mt-3 text-sm leading-5 text-brand-muted">{item.message ?? t("applications.noMessage")}</p><p className="mt-3 text-xs text-brand-muted">{new Intl.DateTimeFormat(locale, { day: "numeric", month: "short" }).format(new Date(item.createdAtUtc))}</p>{canAcceptCampaignApplication(item.status) && <div className="mt-4 grid grid-cols-2 gap-2"><Button disabled={deciding} onClick={() => setDecision({ item, value: "reject" })} type="button" variant="secondary">{t("applications.reject")}</Button><Button disabled={deciding} onClick={() => setDecision({ item, value: "accept" })} type="button">{t("applications.accept")}</Button></div>}</div></div></Card>)}{catalog.hasMore && <div aria-hidden="true" ref={sentinelRef} />}{catalog.loadingMore && <SearchSkeleton compact count={2} />}{catalog.loadMoreFailed && <CatalogState compact icon="refresh" onRetry={() => void catalog.load(catalog.page + 1, true)} subtitle={t("applications.errorSubtitle")} title={t("applications.errorTitle")} />}{catalog.loadedInitialResult && !catalog.hasMore && catalog.items.length > 0 && <p className="catalog-search__end">{t("applications.end")}</p>}</section><Modal onClose={() => !deciding && setDecision(null)} open={Boolean(decision)} title={t(decision?.value === "accept" ? "applications.acceptTitle" : "applications.rejectTitle")}>{decision && <><p className="text-sm leading-6 text-brand-muted">{t(decision.value === "accept" ? "applications.acceptDescription" : "applications.rejectDescription")}</p><div className="mt-5 grid grid-cols-2 gap-3"><Button disabled={deciding} onClick={() => setDecision(null)} type="button" variant="secondary">{t("common.cancel")}</Button><Button disabled={deciding} onClick={decide} type="button" variant={decision.value === "reject" ? "danger" : "primary"}>{deciding ? t("applications.deciding") : t(decision.value === "accept" ? "applications.accept" : "applications.reject")}</Button></div></>}</Modal><Toast message={toast} tone={toastTone} /><BottomNav /></div>;
}

function statusOptions(t: (key: string) => string): string[][] { return [["", t("applications.status.all")], ...([0, 1, 2, 3, 4] as CampaignApplicationStatus[]).map((status) => [String(status), t(campaignApplicationStatusLabelKey(status))])]; }
