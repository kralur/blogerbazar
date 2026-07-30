import { useEffect, useState } from "react";
import {
  acceptCampaignApplication,
  completeDeal,
  createDealReview,
  getMyCampaignApplications,
  getMyDeals,
  type MyCampaignApplication,
  type MyDeal
} from "../api/marketplace";
import { Avatar, Badge, BottomNav, Button, Card, EmptyState, ErrorState, Icon, LoadingState, Modal, Textarea, Toast } from "../components/ui";
import { useI18n } from "../i18n";

const applicationStatusTone = (status: number) => status === 0 ? "blue" : status === 1 ? "green" : status === 2 ? "gray" : "purple";
const dealStatusTone = (status: number) => status === 0 ? "blue" : status === 1 ? "green" : "gray";
const formatDate = (value: string, locale: string) => new Intl.DateTimeFormat(locale, { day: "numeric", month: "short" }).format(new Date(value));

export function MyRequests() {
  const { language, t } = useI18n();
  const applicationStatusLabels: Record<number, string> = { 0: t("requests.applicationNew"), 1: t("requests.applicationAccepted"), 2: t("requests.applicationRejected"), 3: t("requests.applicationWithdrawn") };
  const dealStatusLabels: Record<number, string> = { 0: t("requests.dealActive"), 1: t("requests.dealCompleted"), 2: t("requests.dealCancelled") };
  const locale = language === "uz" ? "uz-UZ" : "ru-RU";
  const [view, setView] = useState<"applications" | "deals">("applications");
  const [requests, setRequests] = useState<MyCampaignApplication[]>([]);
  const [deals, setDeals] = useState<MyDeal[]>([]);
  const [selectedRequest, setSelectedRequest] = useState<MyCampaignApplication | null>(null);
  const [selectedDeal, setSelectedDeal] = useState<MyDeal | null>(null);
  const [rating, setRating] = useState(5);
  const [comment, setComment] = useState("");
  const [loading, setLoading] = useState(true);
  const [failed, setFailed] = useState(false);
  const [toast, setToast] = useState("");

  const load = () => {
    setLoading(true);
    setFailed(false);
    Promise.all([getMyCampaignApplications(), getMyDeals()])
      .then(([nextRequests, nextDeals]) => {
        setRequests(nextRequests);
        setDeals(nextDeals);
      })
      .catch((error) => { setFailed(true); setToast(error instanceof Error ? error.message : t("requests.loadFailed")); })
      .finally(() => setLoading(false));
  };

  useEffect(load, []);

  const accept = async (id: string) => {
    try {
      await acceptCampaignApplication(id);
      setRequests((current) => current.map((request) => request.id === id ? { ...request, status: 1, canAccept: false } : request));
      setSelectedRequest(null);
      setToast(t("requests.accepted"));
      load();
    } catch (error) {
      setToast(error instanceof Error ? error.message : t("requests.acceptFailed"));
    }
  };

  const finishDeal = async (id: string) => {
    try {
      await completeDeal(id);
      setDeals((current) => current.map((deal) => deal.id === id ? { ...deal, status: 1, canComplete: false, canReview: true, completedAtUtc: new Date().toISOString() } : deal));
      setSelectedDeal((current) => current?.id === id ? { ...current, status: 1, canComplete: false, canReview: true, completedAtUtc: new Date().toISOString() } : current);
      setToast(t("requests.completedToast"));
    } catch (error) {
      setToast(error instanceof Error ? error.message : t("requests.completeFailed"));
    }
  };

  const submitReview = async () => {
    if (!selectedDeal) return;

    try {
      await createDealReview(selectedDeal.id, rating, comment);
      setDeals((current) => current.map((deal) => deal.id === selectedDeal.id ? { ...deal, canReview: false } : deal));
      setSelectedDeal((current) => current ? { ...current, canReview: false } : null);
      setComment("");
      setToast(t("requests.reviewPublished"));
    } catch (error) {
      setToast(error instanceof Error ? error.message : t("requests.reviewFailed"));
    }
  };

  return (
    <div className="screen pb-28">
      <header className="flex items-center justify-between pt-2">
        <div>
          <p className="text-sm font-semibold text-brand-muted">{t("requests.eyebrow")}</p>
          <h1 className="text-3xl font-extrabold tracking-tight">{t("requests.title")}</h1>
        </div>
        <span className="grid h-11 w-11 place-items-center rounded-2xl bg-blue-50 text-brand-blue"><Icon name="briefcase" /></span>
      </header>

      <div className="mt-5 grid grid-cols-2 rounded-2xl bg-slate-100 p-1">
        <button className={`rounded-xl py-2.5 text-sm font-bold transition ${view === "applications" ? "bg-white text-brand-ink shadow-sm" : "text-brand-muted"}`} onClick={() => setView("applications")} type="button">{t("requests.applications")}</button>
        <button className={`rounded-xl py-2.5 text-sm font-bold transition ${view === "deals" ? "bg-white text-brand-ink shadow-sm" : "text-brand-muted"}`} onClick={() => setView("deals")} type="button">{t("requests.deals")}</button>
      </div>

      {loading ? <div className="mt-5"><LoadingState title={t("requests.loading")} /></div> : failed ? <div className="mt-5"><ErrorState onRetry={load} subtitle={t("requests.loadFailed")} title={t("requests.loadFailed")} /></div> : view === "applications" ? (
        !requests.length ? <div className="mt-8"><EmptyState subtitle={t("requests.emptyApplicationsSubtitle")} title={t("requests.emptyApplicationsTitle")} /></div> : (
          <div className="mt-5 grid gap-3">
            {requests.map((request) => (
              <button className="text-left" key={request.id} onClick={() => setSelectedRequest(request)} type="button">
                <Card><div className="flex gap-3"><Avatar name={request.counterpartyName} size="sm" /><div className="min-w-0 flex-1"><div className="flex items-center justify-between gap-2"><h2 className="truncate font-extrabold">{request.counterpartyName}</h2><Badge tone={applicationStatusTone(request.status)}>{applicationStatusLabels[request.status]}</Badge></div><p className="mt-1 truncate text-sm text-brand-muted">{request.campaignTitle}</p><p className="mt-2 text-xs text-brand-muted">{formatDate(request.createdAtUtc, locale)}</p></div></div></Card>
              </button>
            ))}
          </div>
        )
      ) : (
        !deals.length ? <div className="mt-8"><EmptyState icon="briefcase" subtitle={t("requests.emptyDealsSubtitle")} title={t("requests.emptyDealsTitle")} /></div> : (
          <div className="mt-5 grid gap-3">
            {deals.map((deal) => (
              <button className="text-left" key={deal.id} onClick={() => setSelectedDeal(deal)} type="button">
                <Card><div className="flex gap-3"><Avatar name={deal.counterpartyName} size="sm" /><div className="min-w-0 flex-1"><div className="flex items-center justify-between gap-2"><h2 className="truncate font-extrabold">{deal.counterpartyName}</h2><Badge tone={dealStatusTone(deal.status)}>{dealStatusLabels[deal.status]}</Badge></div><p className="mt-1 truncate text-sm text-brand-muted">{deal.title}</p><p className="mt-2 text-xs text-brand-muted">{deal.status === 1 && deal.completedAtUtc ? `${t("requests.completed")} ${formatDate(deal.completedAtUtc, locale)}` : `${t("requests.started")} ${formatDate(deal.createdAtUtc, locale)}`}</p></div></div></Card>
              </button>
            ))}
          </div>
        )
      )}

      <Modal onClose={() => setSelectedRequest(null)} open={Boolean(selectedRequest)} title={selectedRequest?.campaignTitle ?? t("requests.title")}>
        {selectedRequest && <><p className="text-sm font-bold">{selectedRequest.counterpartyName}</p><p className="mt-2 text-sm leading-6 text-brand-muted">{selectedRequest.message ?? t("requests.noMessage")}</p>{selectedRequest.canAccept && selectedRequest.status === 0 ? <Button className="mt-4 w-full" onClick={() => accept(selectedRequest.id)}>{t("requests.acceptAction")}</Button> : <p className="mt-4 text-sm text-brand-muted">{t("requests.status")}: {applicationStatusLabels[selectedRequest.status]}</p>}</>}
      </Modal>

      <Modal onClose={() => setSelectedDeal(null)} open={Boolean(selectedDeal)} title={selectedDeal?.title ?? t("requests.deals")}>
        {selectedDeal && <><p className="text-sm font-bold">{selectedDeal.counterpartyName}</p><p className="mt-2 text-sm text-brand-muted">{t("requests.status")}: {dealStatusLabels[selectedDeal.status]}</p>{selectedDeal.canComplete && <Button className="mt-4 w-full" onClick={() => finishDeal(selectedDeal.id)}>{t("requests.complete")}</Button>}{selectedDeal.canReview && <div className="mt-5 border-t border-brand-line pt-5"><p className="text-sm font-extrabold">{t("requests.reviewTitle")}</p><div className="mt-3 flex gap-1">{[1, 2, 3, 4, 5].map((value) => <button aria-label={`${t("requests.rating")} ${value}`} className={`grid h-10 w-10 place-items-center rounded-xl text-xl ${value <= rating ? "bg-amber-50 text-amber-500" : "bg-slate-100 text-slate-300"}`} key={value} onClick={() => setRating(value)} type="button">★</button>)}</div><Textarea className="mt-3" maxLength={1000} onChange={(event) => setComment(event.target.value)} placeholder={t("requests.reviewPlaceholder")} value={comment} /><Button className="mt-3 w-full" onClick={submitReview}>{t("requests.publishReview")}</Button></div>}</>}
      </Modal>

      <Toast message={toast} />
      <BottomNav />
    </div>
  );
}
