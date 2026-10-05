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
import { Avatar, Badge, BottomNav, BottomSheet, Button, Card, EmptyState, ErrorState, Icon, Input, LoadingState, Modal, Toast } from "../components/ui";
import { LanguageSwitcher } from "../components/LanguageSwitcher";
import { useI18n } from "../i18n";
import { useScrollRestoration } from "../hooks/useScrollRestoration";
import { useProfileDataRefresh } from "../hooks/useProfileDataRefresh";
import { CampaignApplicationStatus, campaignApplicationStatusTone, canAcceptCampaignApplication } from "../lib/campaignApplicationStatus";
import { BloggerApplications } from "./BloggerApplications";
import { subscribeDealCache } from "../data/dealCache";
import { dealRoute, dealStatusLabelKey, dealStatusTone } from "../lib/dealStatus";
import { offerFormatLabelKey, offerRoute, offerStateLabelKey, offerStateTone } from "../lib/offerStatus";

const formatDate = (value: string, locale: string) => new Intl.DateTimeFormat(locale, { day: "numeric", month: "short" }).format(new Date(value));

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
  const locale = language === "uz" ? "uz-UZ" : "ru-RU";
  const [view, setView] = useState<"applications" | "offers" | "deals">("applications");
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
      .catch((error) => { setDealsFailed(true); setToastTone("error"); setToast(error instanceof Error ? error.message : t("requests.loadFailed")); })
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
      .catch((error) => { setRequestsFailed(true); setToastTone("error"); setToast(error instanceof Error ? error.message : t("requests.loadFailed")); })
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
      <header className="flex items-center justify-between">
        <div>
          <p className="text-sm font-semibold text-brand-muted">{t("requests.eyebrow")}</p>
          <h1 className="text-3xl font-extrabold tracking-tight">{t("requests.title")}</h1>
        </div>
        <div className="flex shrink-0 items-center gap-2"><LanguageSwitcher />{view === "deals" && <button aria-label={t("requests.dateFilter")} className="grid h-11 w-11 place-items-center rounded-2xl bg-blue-50 text-brand-blue" onClick={() => setDateFilterOpen(true)} type="button"><Icon name="calendar" /></button>}</div>
      </header>

      <div className="mt-5 grid grid-cols-3 rounded-2xl bg-slate-100 p-1">
        <button className={`rounded-xl py-2.5 text-sm font-bold transition ${view === "applications" ? "bg-white text-brand-ink shadow-sm" : "text-brand-muted"}`} onClick={() => setView("applications")} type="button">{t("requests.applications")}</button>
        <button className={`rounded-xl py-2.5 text-sm font-bold transition ${view === "offers" ? "bg-white text-brand-ink shadow-sm" : "text-brand-muted"}`} onClick={() => setView("offers")} type="button">{t("offers.tab")}</button>
        <button className={`rounded-xl py-2.5 text-sm font-bold transition ${view === "deals" ? "bg-white text-brand-ink shadow-sm" : "text-brand-muted"}`} onClick={() => setView("deals")} type="button">{t("requests.deals")}</button>
      </div>

      {view === "offers" ? (
        offersLoading ? <div className="mt-5"><LoadingState title={t("offers.loading")} /></div> : offersFailed ? <div className="mt-5"><ErrorState onRetry={loadOffers} subtitle={t("offers.errorSubtitle")} title={t("offers.errorTitle")} /></div> : !offers.length ? <div className="mt-8"><EmptyState icon="send" subtitle={t(activeMarketplaceRole === "Blogger" ? "offers.emptyBloggerSubtitle" : "offers.emptyBusinessSubtitle")} title={t("offers.emptyTitle")} /></div> : (
          <div className="mt-5 grid gap-3">
            {offers.map((offer) => (
              <a className="block text-left" href={`#${offerRoute(offer.id)}`} key={offer.id}>
                <Card><div className="flex gap-3"><Avatar name={offer.counterpartyName} size="sm" src={offer.counterpartyImageUrl} /><div className="min-w-0 flex-1"><div className="flex items-center justify-between gap-2"><h2 className="truncate font-extrabold">{offer.counterpartyName}</h2><Badge tone={offerStateTone(offer.state)}>{t(offerStateLabelKey(offer.state))}</Badge></div><p className="mt-1 truncate text-sm text-brand-muted">{t(offerFormatLabelKey(offer.format))}</p><p className="mt-2 text-xs text-brand-muted">{formatDate(offer.createdAtUtc, locale)}</p></div></div></Card>
              </a>
            ))}
          </div>
        )
      ) : view === "applications" && activeMarketplaceRole === "Blogger" ? <BloggerApplications activeMarketplaceRole={activeMarketplaceRole} /> : view === "applications" ? (
        requestsLoading ? <div className="mt-5"><LoadingState title={t("requests.loading")} /></div> : requestsFailed ? <div className="mt-5"><ErrorState onRetry={loadRequests} subtitle={t("requests.loadFailed")} title={t("requests.loadFailed")} /></div> :
        !visibleRequests.length ? <div className="mt-8"><EmptyState subtitle={requests.length ? t("requests.emptyDateSubtitle") : t("requests.emptyApplicationsSubtitle")} title={requests.length ? t("requests.emptyDateTitle") : t("requests.emptyApplicationsTitle")} /></div> : (
          <div className="mt-5 grid gap-3">
            {visibleRequests.map((request) => (
              <button className="text-left" key={request.id} onClick={() => setSelectedRequest(request)} type="button">
                <Card><div className="flex gap-3"><Avatar name={request.counterpartyName} size="sm" src={request.counterpartyImageUrl} /><div className="min-w-0 flex-1"><div className="flex items-center justify-between gap-2"><h2 className="truncate font-extrabold">{request.counterpartyName}</h2><Badge tone={campaignApplicationStatusTone(request.status)}>{applicationStatusLabels[request.status]}</Badge></div><p className="mt-1 truncate text-sm text-brand-muted">{request.campaignTitle}</p><p className="mt-2 text-xs text-brand-muted">{formatDate(request.createdAtUtc, locale)}</p></div></div></Card>
              </button>
            ))}
          </div>
        )
      ) : (
        dealsLoading ? <div className="mt-5"><LoadingState title={t("requests.loading")} /></div> : dealsFailed ? <div className="mt-5"><ErrorState onRetry={loadDeals} subtitle={t("requests.loadFailed")} title={t("requests.loadFailed")} /></div> : !visibleDeals.length ? <div className="mt-8"><EmptyState icon="briefcase" subtitle={deals.length ? t("requests.emptyDateSubtitle") : t("requests.emptyDealsSubtitle")} title={deals.length ? t("requests.emptyDateTitle") : t("requests.emptyDealsTitle")} /></div> : (
          <div className="mt-5 grid gap-3">
            {visibleDeals.map((deal) => (
              <a className="block text-left" href={`#${dealRoute(deal.id)}`} key={deal.id}>
                <Card><div className="flex gap-3"><Avatar name={deal.counterpartyName} size="sm" src={deal.counterpartyImageUrl} /><div className="min-w-0 flex-1"><div className="flex items-center justify-between gap-2"><h2 className="truncate font-extrabold">{deal.counterpartyName}</h2><Badge tone={dealStatusTone(deal.status)}>{t(dealStatusLabelKey(deal.status))}</Badge></div><p className="mt-1 truncate text-sm text-brand-muted">{deal.sourceType === "collaborationRequest" ? t("deals.source.collaborationRequest") : deal.title}</p><p className="mt-2 text-xs text-brand-muted">{deal.status === 1 && deal.completedAtUtc ? `${t("requests.completed")} ${formatDate(deal.completedAtUtc, locale)}` : `${t("requests.started")} ${formatDate(deal.createdAtUtc, locale)}`}</p></div></div></Card>
              </a>
            ))}
          </div>
        )
      )}

      <Modal onClose={() => setSelectedRequest(null)} open={Boolean(selectedRequest)} title={selectedRequest?.campaignTitle ?? t("requests.title")}>
        {selectedRequest && <><p className="text-sm font-bold">{selectedRequest.counterpartyName}</p><p className="mt-2 text-sm leading-6 text-brand-muted">{selectedRequest.message ?? t("requests.noMessage")}</p>{selectedRequest.canAccept && canAcceptCampaignApplication(selectedRequest.status) ? <Button className="mt-4 w-full" onClick={() => accept(selectedRequest.id)}>{t("requests.acceptAction")}</Button> : <p className="mt-4 text-sm text-brand-muted">{t("requests.status")}: {applicationStatusLabels[selectedRequest.status]}</p>}</>}
      </Modal>

      <BottomSheet onClose={() => setDateFilterOpen(false)} open={dateFilterOpen} title={t("requests.dateFilter")}><div className="grid gap-3"><div className="grid grid-cols-2 gap-2">{(["today", "week", "month", "custom"] as const).map((range) => <button className={`rounded-2xl border px-3 py-3 text-sm font-bold ${dateRange === range ? "border-brand-blue bg-blue-50 text-brand-blue" : "border-brand-line bg-white"}`} key={range} onClick={() => setDateRange(range)} type="button">{t(`requests.range.${range}`)}</button>)}</div>{dateRange === "custom" && <div className="grid grid-cols-2 gap-3"><Input label={t("requests.fromDate")} onChange={(event) => setFromDate(event.target.value)} type="date" value={fromDate} /><Input label={t("requests.toDate")} onChange={(event) => setToDate(event.target.value)} type="date" value={toDate} /></div>}<Button className="w-full" onClick={() => setDateFilterOpen(false)} type="button">{t("common.apply")}</Button><Button className="w-full" onClick={() => { setDateRange("all"); setFromDate(""); setToDate(""); setDateFilterOpen(false); }} type="button" variant="secondary">{t("common.reset")}</Button></div></BottomSheet>

      <Toast message={toast} tone={toastTone} />
      <BottomNav />
    </div>
  );
}
