import { useCallback, useEffect, useState } from "react";
import {
  acceptCampaignApplication,
  getMyCampaignApplications,
  getMyDeals,
  getMyOffers,
  type MarketplaceRole,
  type MyCampaignApplication,
  type MyDeal,
  type Offer
} from "../api/marketplace";
import { Badge, BottomNav, BottomSheet, Button, EmptyState, ErrorState, Icon, Input, LoadingState, Modal, Toast } from "../components/ui";
import { useI18n, type Language } from "../i18n";
import { useScrollRestoration } from "../hooks/useScrollRestoration";
import { useProfileDataRefresh } from "../hooks/useProfileDataRefresh";
import { CampaignApplicationStatus, campaignApplicationStatusTone, canAcceptCampaignApplication } from "../lib/campaignApplicationStatus";
import { BloggerApplications } from "./BloggerApplications";
import { subscribeDealCache } from "../data/dealCache";
import { dealRoute, dealStatusLabelKey, dealStatusTone } from "../lib/dealStatus";
import { offerFormatLabelKey, offerRoute, offerStateLabelKey, offerStateTone } from "../lib/offerStatus";
import { PageHeader } from "../components/PageHeader";
import { RequestRow } from "../components/RequestRow";
import { formatShortDate, formatCurrency } from "../lib/currency";
import { useScreenRefresh } from "../hooks/useScreenRefresh";
import { ActionBadge, useActionCounts } from "../features/actionCounts/ActionCountsProvider";

const formatDate = (value: string, language: Language) => formatShortDate(value, language);

export function MyRequests({ activeMarketplaceRole }: { activeMarketplaceRole?: MarketplaceRole }) {
  const { language, t } = useI18n();
  useScrollRestoration("requests");
  const applicationStatusLabels: Record<CampaignApplicationStatus, string> = {
    [CampaignApplicationStatus.Sent]: t("requests.applicationSent"),
    [CampaignApplicationStatus.Viewed]: t("requests.applicationViewed"),
    [CampaignApplicationStatus.Accepted]: t("requests.applicationAccepted"),
    [CampaignApplicationStatus.Rejected]: t("requests.applicationRejected"),
    [CampaignApplicationStatus.Withdrawn]: t("requests.applicationWithdrawn")
  };
  const actionCounts = useActionCounts();
  const [view, setView] = useState<RequestsView>(() => hashRequestsView() ?? "applications");
  // Home activity links open a specific tab: #/requests?tab=deals|offers|applications
  useEffect(() => {
    const syncTab = () => { const tab = hashRequestsView(); if (tab) setView(tab); };
    window.addEventListener("hashchange", syncTab);
    return () => window.removeEventListener("hashchange", syncTab);
  }, []);
  const [offers, setOffers] = useState<Offer[]>([]);
  const [offersLoading, setOffersLoading] = useState(true);
  const [offersFailed, setOffersFailed] = useState(false);
  const [requests, setRequests] = useState<MyCampaignApplication[]>([]);
  const [deals, setDeals] = useState<MyDeal[]>([]);
  const [selectedRequest, setSelectedRequest] = useState<MyCampaignApplication | null>(null);
  const [requestsLoading, setRequestsLoading] = useState(activeMarketplaceRole !== "Blogger");
  const [requestsFailed, setRequestsFailed] = useState(false);
  const [dealsLoading, setDealsLoading] = useState(true);
  const [dealsFailed, setDealsFailed] = useState(false);
  const [toast, setToast] = useState("");
  const [toastTone, setToastTone] = useState<"success" | "error">("success");
  const [dateFilterOpen, setDateFilterOpen] = useState(false);
  const [dateRange, setDateRange] = useState<"today" | "week" | "month" | "custom" | "all">("all");
  const [fromDate, setFromDate] = useState("");
  const [toDate, setToDate] = useState("");

  const loadDeals = useCallback(() => {
    setDealsLoading(true);
    setDealsFailed(false);
    void getMyDeals()
      .then(setDeals)
      .catch(() => setDealsFailed(true))
      .finally(() => setDealsLoading(false));
  }, [t]);

  const loadRequests = useCallback(() => {
    if (activeMarketplaceRole === "Blogger") {
      setRequests([]);
      setRequestsFailed(false);
      setRequestsLoading(false);
      return;
    }
    setRequestsLoading(true);
    setRequestsFailed(false);
    void getMyCampaignApplications()
      .then(setRequests)
      .catch(() => setRequestsFailed(true))
      .finally(() => setRequestsLoading(false));
  }, [activeMarketplaceRole, t]);

  const loadOffers = useCallback(() => {
    setOffersLoading(true);
    setOffersFailed(false);
    void getMyOffers()
      .then(setOffers)
      .catch(() => setOffersFailed(true))
      .finally(() => setOffersLoading(false));
  }, []);

  const load = useCallback(() => {
    loadDeals();
    loadRequests();
    loadOffers();
  }, [loadDeals, loadOffers, loadRequests]);

  useEffect(() => { load(); }, [load]);
  useEffect(() => subscribeDealCache((details) => {
    if (!details) return;
    setDeals((current) => current.map((deal) => deal.id === details.id ? { ...deal, status: details.status, completedAtUtc: details.completedAtUtc, canComplete: details.canComplete, canReview: details.canReview } : deal));
  }), []);
  useProfileDataRefresh(load);
  useScreenRefresh(load);

  const withinRange = (value: string) => {
    const date = new Date(value);
    if (dateRange === "all") return true;
    if (dateRange === "custom") return (!fromDate || date >= new Date(`${fromDate}T00:00:00`)) && (!toDate || date <= new Date(`${toDate}T23:59:59`));
    const days = dateRange === "today" ? 1 : dateRange === "week" ? 7 : 30;
    return date.getTime() >= Date.now() - days * 86_400_000;
  };
  const visibleRequests = requests.filter((request) => withinRange(request.createdAtUtc));
  const visibleDeals = deals.filter((deal) => withinRange(deal.createdAtUtc));

  const accept = async (id: string) => {
    try {
      await acceptCampaignApplication(id);
      setRequests((current) => current.map((request) => request.id === id ? { ...request, status: CampaignApplicationStatus.Accepted, canAccept: false } : request));
      setSelectedRequest(null);
      setToastTone("success");
      setToast(t("requests.accepted"));
      load();
    } catch (error) {
      setToastTone("error");
      setToast(error instanceof Error ? error.message : t("requests.acceptFailed"));
    }
  };

  return (
    <div className="screen screen--with-nav">
      <PageHeader actions={view === "deals" ? <button aria-label={t("requests.dateFilter")} className="page-header__icon-button" onClick={() => setDateFilterOpen(true)} type="button"><Icon name="calendar" /></button> : undefined} eyebrow={t("requests.eyebrow")} title={t("requests.title")} />

      <div aria-label={t("requests.title")} className="catalog-search__segments catalog-search__segments--three" role="group">
        {([["applications", t("requests.applications"), actionCounts.applications], ["offers", t("offers.tab"), actionCounts.offers], ["deals", t("requests.deals"), actionCounts.reviews]] as const).map(([value, label, count]) => <button aria-pressed={view === value} className={`catalog-search__segment${view === value ? " catalog-search__segment--selected" : ""}`} key={value} onClick={() => setView(value)} type="button">{label}<ActionBadge count={count} label={t("requests.actionBadge", { count })} /></button>)}
      </div>

      {view === "offers" ? (
        offersLoading ? <div className="mt-5"><LoadingState title={t("offers.loading")} /></div> : offersFailed ? <div className="mt-5"><ErrorState onRetry={loadOffers} subtitle={t("offers.errorSubtitle")} title={t("offers.errorTitle")} /></div> : !offers.length ? <div className="mt-8"><EmptyState icon="send" subtitle={t(activeMarketplaceRole === "Blogger" ? "offers.emptyBloggerSubtitle" : "offers.emptyBusinessSubtitle")} title={t("offers.emptyTitle")} /></div> : (
          <div className="request-list">
            {offers.map((offer) => (
              <RequestRow href={`#${offerRoute(offer.id)}`} imageUrl={offer.counterpartyImageUrl} key={offer.id} meta={formatDate(offer.createdAtUtc, language)} name={offer.counterpartyName} status={<Badge tone={offerStateTone(offer.state)}>{t(offerStateLabelKey(offer.state))}</Badge>} title={`${t(offerFormatLabelKey(offer.format))}${offer.offeredBudget != null ? ` · ${formatCurrency(offer.offeredBudget)}` : ""}`} />
            ))}
          </div>
        )
      ) : view === "applications" && activeMarketplaceRole === "Blogger" ? <BloggerApplications activeMarketplaceRole={activeMarketplaceRole} /> : view === "applications" ? (
        requestsLoading ? <div className="mt-5"><LoadingState title={t("requests.loading")} /></div> : requestsFailed ? <div className="mt-5"><ErrorState onRetry={loadRequests} title={t("requests.loadFailed")} /></div> :
        !visibleRequests.length ? <div className="mt-8"><EmptyState subtitle={requests.length ? t("requests.emptyDateSubtitle") : t("requests.emptyApplicationsSubtitle")} title={requests.length ? t("requests.emptyDateTitle") : t("requests.emptyApplicationsTitle")} /></div> : (
          <div className="request-list">
            {visibleRequests.map((request) => (
              <RequestRow imageUrl={request.counterpartyImageUrl} key={request.id} meta={formatDate(request.createdAtUtc, language)} name={request.counterpartyName} onClick={() => setSelectedRequest(request)} status={<Badge tone={campaignApplicationStatusTone(request.status)}>{applicationStatusLabels[request.status]}</Badge>} title={request.campaignTitle} />
            ))}
          </div>
        )
      ) : (
        dealsLoading ? <div className="mt-5"><LoadingState title={t("requests.loading")} /></div> : dealsFailed ? <div className="mt-5"><ErrorState onRetry={loadDeals} title={t("requests.loadFailed")} /></div> : !visibleDeals.length ? <div className="mt-8"><EmptyState icon="briefcase" subtitle={deals.length ? t("requests.emptyDateSubtitle") : t("requests.emptyDealsSubtitle")} title={deals.length ? t("requests.emptyDateTitle") : t("requests.emptyDealsTitle")} /></div> : (
          <div className="request-list">
            {visibleDeals.map((deal) => (
              <RequestRow href={`#${dealRoute(deal.id)}`} imageUrl={deal.counterpartyImageUrl} key={deal.id} meta={deal.canReview ? t("deals.awaitingReview") : deal.status === 1 && deal.completedAtUtc ? `${t("requests.completed")} ${formatDate(deal.completedAtUtc, language)}` : `${t("requests.started")} ${formatDate(deal.createdAtUtc, language)}`} name={deal.counterpartyName} status={<Badge tone={dealStatusTone(deal.status)}>{t(dealStatusLabelKey(deal.status))}</Badge>} title={deal.sourceType === "collaborationRequest" ? t("deals.source.collaborationRequest") : deal.title} />
            ))}
          </div>
        )
      )}

      <Modal onClose={() => setSelectedRequest(null)} open={Boolean(selectedRequest)} title={selectedRequest?.campaignTitle ?? t("requests.title")}>
        {selectedRequest && <><p className="text-sm font-bold">{selectedRequest.counterpartyName}</p><p className="mt-2 text-sm leading-6 text-brand-muted">{selectedRequest.message ?? t("requests.noMessage")}</p>{selectedRequest.canAccept && canAcceptCampaignApplication(selectedRequest.status) ? <Button className="mt-4 w-full" onClick={() => accept(selectedRequest.id)}>{t("requests.acceptAction")}</Button> : <p className="mt-4 text-sm text-brand-muted">{t("requests.status")}: {applicationStatusLabels[selectedRequest.status]}</p>}</>}
      </Modal>

      <BottomSheet onClose={() => setDateFilterOpen(false)} open={dateFilterOpen} title={t("requests.dateFilter")}><div className="grid gap-3"><div className="grid grid-cols-2 gap-2">{(["today", "week", "month", "custom"] as const).map((range) => <button className={`rounded-2xl border px-3 py-3 text-sm font-bold ${dateRange === range ? "choice-selected" : "border-brand-line bg-brand-surface"}`} key={range} onClick={() => setDateRange(range)} type="button">{t(`requests.range.${range}`)}</button>)}</div>{dateRange === "custom" && <div className="grid grid-cols-2 gap-3"><Input label={t("requests.fromDate")} onChange={(event) => setFromDate(event.target.value)} type="date" value={fromDate} /><Input label={t("requests.toDate")} onChange={(event) => setToDate(event.target.value)} type="date" value={toDate} /></div>}<Button className="w-full" onClick={() => setDateFilterOpen(false)} type="button">{t("common.apply")}</Button><Button className="w-full" onClick={() => { setDateRange("all"); setFromDate(""); setToDate(""); setDateFilterOpen(false); }} type="button" variant="secondary">{t("common.reset")}</Button></div></BottomSheet>

      <Toast message={toast} tone={toastTone} />
      <BottomNav />
    </div>
  );
}

type RequestsView = "applications" | "offers" | "deals";

function hashRequestsView(): RequestsView | null {
  if (typeof window === "undefined" || !window.location.hash.startsWith("#/requests")) return null;
  const tab = new URLSearchParams(window.location.hash.split("?")[1] ?? "").get("tab");
  return tab === "applications" || tab === "offers" || tab === "deals" ? tab : null;
}
