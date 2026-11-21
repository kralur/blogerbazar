import { useSectionBack } from "../lib/desktopLayout";
import { useBusinessCanOperate } from "../hooks/useBusinessCanOperate";
import { useCallback, useEffect, useState } from "react";
import {
  acceptCampaignApplication,
  getMyCampaignApplications,
  getMyDeals,
  getMyOffers,
  rejectMyCampaignApplication,
  type MarketplaceRole,
  type MyCampaignApplication,
  type MyDeal,
  type Offer
} from "../api/marketplace";
import { Badge, BottomNav, BottomSheet, Button, EmptyState, ErrorState, Icon, Input, Modal, Skeleton, Toast } from "../components/ui";
import { useI18n, type Language } from "../i18n";
import { useScrollRestoration } from "../hooks/useScrollRestoration";
import { useProfileDataRefresh } from "../hooks/useProfileDataRefresh";
import { CampaignApplicationStatus, campaignApplicationStatusTone, canAcceptCampaignApplication } from "../lib/campaignApplicationStatus";
import { BloggerApplications } from "./BloggerApplications";
import { profileRoute } from "../lib/profileRoutes";
import { subscribeDealCache } from "../data/dealCache";
import { seenDealsChangedEvent, unseenActiveDealIds } from "../data/seenDeals";
import { dealRoute, dealStatusLabelKey, dealStatusTone } from "../lib/dealStatus";
import { offerFormatLabelKey, offerRoute, offerStateLabelKey, offerStateTone } from "../lib/offerStatus";
import { ListHero } from "../components/ListHero";
import { PageHeader } from "../components/PageHeader";
import { RequestRow } from "../components/RequestRow";
import { CounterpartyRow } from "../components/CounterpartyRow";
import { formatNumber, formatShortDate } from "../lib/currency";
import { useScreenRefresh } from "../hooks/useScreenRefresh";
import { ActionBadge, notifyActionCountsChanged, useActionCounts } from "../features/actionCounts/ActionCountsProvider";
import { ApiError, getApiErrorMessage } from "../api/client";

const formatDate = (value: string, language: Language) => formatShortDate(value, language);

// Row-shaped placeholders while a list loads: the screen keeps the shape it is about to have.
function RequestListSkeleton({ label }: { label: string }) {
  return <div aria-busy="true" aria-label={label} className="request-list-skeleton" role="status"><span className="sr-only">{label}</span>{[0, 1, 2].map((row) => <Skeleton className="request-list-skeleton__row" key={row} />)}</div>;
}

// canAccept only says the viewer is the business side; the application waits for an answer while it is still pending (QA Q19).
function awaitsAnswer(request: MyCampaignApplication) {
  return request.canAccept && canAcceptCampaignApplication(request.status);
}

export function MyRequests({ activeMarketplaceRole }: { activeMarketplaceRole?: MarketplaceRole }) {
  const { language, t } = useI18n();
  const sectionBack = useSectionBack(t("nav.home"));
  // A team viewer (D64) sees the company's applications without Accept / Reject.
  const canOperate = useBusinessCanOperate(activeMarketplaceRole === "Business");
  useScrollRestoration("requests");
  const applicationStatusLabels: Record<CampaignApplicationStatus, string> = {
    // The company received it: "new", not "sent".
    [CampaignApplicationStatus.Sent]: t("applications.status.new"),
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
  // Re-read which deals are new after one is opened.
  const [seenTick, setSeenTick] = useState(0);
  useEffect(() => {
    const onSeen = () => setSeenTick((tick) => tick + 1);
    window.addEventListener(seenDealsChangedEvent, onSeen);
    return () => window.removeEventListener(seenDealsChangedEvent, onSeen);
  }, []);
  const [selectedRequest, setSelectedRequest] = useState<MyCampaignApplication | null>(null);
  // The requests screen stays mounted under other screens: leaving it (e.g. the applicant's profile link) closes the sheet (QA Q18).
  useEffect(() => {
    const close = () => setSelectedRequest(null);
    window.addEventListener("hashchange", close);
    return () => window.removeEventListener("hashchange", close);
  }, []);
  // A brand face applies to campaigns like a blogger (D46); the business sees the inbox list instead.
  const isCreator = activeMarketplaceRole === "Blogger" || activeMarketplaceRole === "BrandFace";
  const [requestsLoading, setRequestsLoading] = useState(!isCreator);
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
    if (isCreator) {
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
  }, [isCreator, t]);

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
  const newDealIds = seenTick >= 0 ? unseenActiveDealIds(deals) : new Set<string>();

  const [pendingDecision, setPendingDecision] = useState<"accept" | "reject" | null>(null);
  const [deciding, setDeciding] = useState(false);
  useEffect(() => { setPendingDecision(null); }, [selectedRequest?.id]);
  const decide = async (request: MyCampaignApplication, value: "accept" | "reject") => {
    setDeciding(true);
    try {
      if (value === "accept") await acceptCampaignApplication(request.id);
      else await rejectMyCampaignApplication(request.campaignId, request.id);
      const status = value === "accept" ? CampaignApplicationStatus.Accepted : CampaignApplicationStatus.Rejected;
      setRequests((current) => current.map((item) => item.id === request.id ? { ...item, status, canAccept: false } : item));
      setSelectedRequest(null);
      setToastTone("success");
      setToast(t(value === "accept" ? "requests.accepted" : "applications.rejectedToast"));
      notifyActionCountsChanged();
      load();
    } catch (error) {
      setToastTone("error");
      setToast(error instanceof ApiError && error.status === 403 ? t("common.noPermission") : getApiErrorMessage(error, t(value === "accept" ? "requests.acceptFailed" : "applications.decisionFailed")));
    } finally {
      setDeciding(false);
    }
  };

  return (
    <div className="screen screen--with-nav">
      <ListHero>
      <PageHeader back={sectionBack} actions={view === "deals" ? <button aria-label={t("requests.dateFilter")} className="page-header__icon-button" onClick={() => setDateFilterOpen(true)} type="button"><Icon name="calendar" /></button> : undefined} eyebrow={t("requests.eyebrow")} title={t("requests.title")} />

      <div aria-label={t("requests.title")} className="catalog-search__segments catalog-search__segments--three" role="group">
        {([["applications", t("requests.applications"), actionCounts.applications], ["offers", t("offers.tab"), actionCounts.offers], ["deals", t("requests.deals"), actionCounts.reviews + actionCounts.newDeals]] as const).map(([value, label, count]) => <button aria-pressed={view === value} className={`catalog-search__segment${view === value ? " catalog-search__segment--selected" : ""}`} key={value} onClick={() => setView(value)} type="button"><span>{label}</span>{view === value && count === 0 && <span aria-hidden="true" className="catalog-search__segment-indicator">✓</span>}<ActionBadge count={count} label={t("requests.actionBadge", { count })} /></button>)}
      </div>
      </ListHero>

      {view === "offers" ? (
        offersLoading ? <RequestListSkeleton label={t("offers.loading")} /> : offersFailed ? <div className="mt-5"><ErrorState onRetry={loadOffers} subtitle={t("offers.errorSubtitle")} title={t("offers.errorTitle")} /></div> : !offers.length ? <div className="mt-8"><EmptyState action={isCreator ? undefined : { label: t("favorites.findBloggers"), href: "#/search" }} icon="send" subtitle={t(isCreator ? "offers.emptyBloggerSubtitle" : "offers.emptyBusinessSubtitle")} title={t("offers.emptyTitle")} /></div> : (
          <div className="request-list">
            {waitingFirst(offers, (offer) => offer.canRespond).map((offer) => (
              <RequestRow action={offer.canRespond ? t("requests.awaitingYourAnswer") : null} href={`#${offerRoute(offer.id)}`} imageUrl={offer.counterpartyImageUrl} key={offer.id} meta={formatDate(offer.createdAtUtc, language)} name={offer.counterpartyName} status={<Badge tone={offerStateTone(offer.state)}>{t(offerStateLabelKey(offer.state))}</Badge>} price={offer.offeredBudget != null ? formatNumber(offer.offeredBudget) : null} priceLabel={t("common.budget")} priceUnit={t("currency.uzs")} title={t(offerFormatLabelKey(offer.format))} />
            ))}
          </div>
        )
      ) : view === "applications" && isCreator ? <BloggerApplications activeMarketplaceRole={activeMarketplaceRole} /> : view === "applications" ? (
        requestsLoading ? <RequestListSkeleton label={t("requests.loading")} /> : requestsFailed ? <div className="mt-5"><ErrorState onRetry={loadRequests} title={t("requests.loadFailed")} /></div> :
        !visibleRequests.length ? <div className="mt-8"><EmptyState action={requests.length ? undefined : { label: t("myCampaigns.title"), href: "#/my-campaigns" }} subtitle={requests.length ? t("requests.emptyDateSubtitle") : t("requests.emptyBusinessApplicationsSubtitle")} title={requests.length ? t("requests.emptyDateTitle") : t("requests.emptyApplicationsTitle")} /></div> : (
          <div className="request-list">
            {waitingFirst(visibleRequests, awaitsAnswer).map((request) => (
              <RequestRow action={awaitsAnswer(request) ? t("requests.awaitingYourAnswer") : null} imageUrl={request.counterpartyImageUrl} key={request.id} meta={formatDate(request.createdAtUtc, language)} name={request.counterpartyName} onClick={() => setSelectedRequest(request)} status={<Badge tone={campaignApplicationStatusTone(request.status)}>{applicationStatusLabels[request.status]}</Badge>} title={request.campaignTitle} />
            ))}
          </div>
        )
      ) : (
        dealsLoading ? <RequestListSkeleton label={t("requests.loading")} /> : dealsFailed ? <div className="mt-5"><ErrorState onRetry={loadDeals} title={t("requests.loadFailed")} /></div> : !visibleDeals.length ? <div className="mt-8"><EmptyState icon="briefcase" subtitle={deals.length ? t("requests.emptyDateSubtitle") : t("requests.emptyDealsSubtitle")} title={deals.length ? t("requests.emptyDateTitle") : t("requests.emptyDealsTitle")} /></div> : (
          <div className="request-list">
            {waitingFirst(visibleDeals, (deal) => deal.canReview || newDealIds.has(deal.id)).map((deal) => (
              <RequestRow action={deal.canReview ? t(deal.partnerHasReviewed ? "deals.partnerReviewedRow" : "deals.awaitingReview") : newDealIds.has(deal.id) ? t("deals.newDealRow") : null} href={`#${dealRoute(deal.id)}`} imageUrl={deal.counterpartyImageUrl} key={deal.id} meta={deal.canReview ? t("deals.awaitingReview") : deal.status === 1 && deal.completedAtUtc ? `${t("requests.completed")} ${formatDate(deal.completedAtUtc, language)}` : `${t("requests.started")} ${formatDate(deal.createdAtUtc, language)}`} name={deal.counterpartyDeleted ? t("common.deletedAccount") : deal.counterpartyName} status={<Badge tone={dealStatusTone(deal.status)}>{t(dealStatusLabelKey(deal.status))}</Badge>} title={deal.sourceType === "collaborationRequest" ? t("deals.source.collaborationRequest") : deal.title} />
            ))}
          </div>
        )
      )}

      <Modal onClose={() => setSelectedRequest(null)} open={Boolean(selectedRequest)} title={selectedRequest?.campaignTitle ?? t("requests.title")}>
        {selectedRequest && <><CounterpartyRow href={selectedRequest.counterpartyProfileId ? profileRoute(selectedRequest.counterpartyRole ?? "blogger", selectedRequest.counterpartyProfileId) : null} imageUrl={selectedRequest.counterpartyImageUrl} label={t(selectedRequest.counterpartyRole === "brandFace" ? "offers.counterpartyBrandFace" : "offers.counterpartyBlogger")} name={selectedRequest.counterpartyName} /><p className="mt-2 text-sm leading-6 text-brand-muted">{selectedRequest.message ?? t("requests.noMessage")}</p>{canOperate && selectedRequest.canAccept && canAcceptCampaignApplication(selectedRequest.status)
          // Accepting creates a deal, so it asks first, as on the campaign's applications page; rejecting is here too.
          ? pendingDecision ? <div className="mt-4 grid gap-2"><p className="text-sm font-semibold">{t(pendingDecision === "accept" ? "applications.acceptDescription" : "applications.rejectDescription")}</p><div className="modal-actions"><Button disabled={deciding} onClick={() => setPendingDecision(null)} type="button" variant="secondary">{t("common.cancel")}</Button><Button aria-busy={deciding} disabled={deciding} onClick={() => void decide(selectedRequest, pendingDecision)} type="button" variant={pendingDecision === "reject" ? "danger" : undefined}>{t(pendingDecision === "accept" ? "applications.accept" : "applications.reject")}</Button></div></div>
            : <div className="mt-4 modal-actions"><Button onClick={() => setPendingDecision("reject")} type="button" variant="secondary">{t("applications.reject")}</Button><Button onClick={() => setPendingDecision("accept")} type="button">{t("applications.accept")}</Button></div>
          : <p className="mt-4 text-sm text-brand-muted">{t("requests.status")}: {applicationStatusLabels[selectedRequest.status]}</p>}<a className="ds-button ds-button--secondary tap-target mt-3 inline-flex w-full items-center justify-center px-5" href={`#/my-campaign-applications/${selectedRequest.campaignId}`} onClick={() => setSelectedRequest(null)}>{t("requests.openCampaignApplications")}</a></>}
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

// Items that wait for the viewer go first; the rest keep their order (Array.sort is stable).
function waitingFirst<T>(items: T[], waiting: (item: T) => boolean) {
  return [...items].sort((a, b) => Number(waiting(b)) - Number(waiting(a)));
}
