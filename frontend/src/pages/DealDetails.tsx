import { useCallback, useEffect, useRef, useState } from "react";
import { ApiError, getApiErrorMessage } from "../api/client";
import { completeDeal, createDealReview, getMyDeal, type DealDetails as Deal } from "../api/marketplace";
import { getCachedDeal, setCachedDeal } from "../data/dealCache";
import { Avatar, Badge, BottomNav, Button, Card, ErrorState, LoadingState, Modal, Textarea, Toast } from "../components/ui";
import { ManagementBackLink } from "../components/ManagementBackLink";
import { categoryLabel, cityLabel, useI18n } from "../i18n";
import { formatCurrency } from "../lib/currency";
import { dealSourceLabelKey, dealStatusLabelKey, dealStatusTone } from "../lib/dealStatus";

type LoadState = "loading" | "ready" | "denied" | "not-found" | "error";

export function DealDetails({ id }: { id: string }) {
  const { language, t } = useI18n();
  const [deal, setDeal] = useState<Deal | null>(() => getCachedDeal(id));
  const [state, setState] = useState<LoadState>(deal ? "ready" : "loading");
  const [completeOpen, setCompleteOpen] = useState(false);
  const [busy, setBusy] = useState(false);
  const [rating, setRating] = useState(5);
  const [comment, setComment] = useState("");
  const [toast, setToast] = useState("");
  const [tone, setTone] = useState<"success" | "error">("success");
  const requestRef = useRef(0);
  const actionRef = useRef(0);
  const busyRef = useRef(false);
  const idRef = useRef(id);
  idRef.current = id;

  const load = useCallback(async () => {
    const requestId = ++requestRef.current;
    const cached = getCachedDeal(id);
    if (!cached) setState("loading");
    try {
      const value = await getMyDeal(id);
      if (requestId !== requestRef.current || idRef.current !== id) return;
      setCachedDeal(value);
      setDeal(value);
      setState("ready");
    } catch (error) {
      if (requestId !== requestRef.current || idRef.current !== id) return;
      const terminal = error instanceof ApiError && error.status === 404 ? "not-found" : error instanceof ApiError && (error.status === 401 || error.status === 403) ? "denied" : null;
      if (terminal) setState(terminal);
      else if (cached) {
        setState("ready");
        setTone("error");
        setToast(t("deals.errorSubtitle"));
      } else setState("error");
    }
  }, [id, t]);

  useEffect(() => {
    setDeal(getCachedDeal(id));
    void load();
    return () => { requestRef.current += 1; actionRef.current += 1; busyRef.current = false; };
  }, [id, load]);

  const runAction = async (action: () => Promise<unknown>, successKey: string, failureKey: string) => {
    if (busyRef.current) return;
    busyRef.current = true;
    const actionId = ++actionRef.current;
    setBusy(true);
    try {
      await action();
      if (actionId !== actionRef.current || idRef.current !== id) return;
      setCompleteOpen(false);
      setComment("");
      setTone("success");
      setToast(t(successKey));
      await load();
    } catch (error) {
      if (actionId !== actionRef.current || idRef.current !== id) return;
      setCompleteOpen(false);
      if (error instanceof ApiError && error.status === 409) await load();
      else if (error instanceof ApiError && error.status === 404) setState("not-found");
      else if (error instanceof ApiError && (error.status === 401 || error.status === 403)) setState("denied");
      else { setTone("error"); setToast(getApiErrorMessage(error, t(failureKey))); }
    } finally {
      if (actionId === actionRef.current && idRef.current === id) { busyRef.current = false; setBusy(false); }
    }
  };

  const complete = () => runAction(() => completeDeal(id), "requests.completedToast", "requests.completeFailed");
  const review = () => runAction(() => createDealReview(id, rating, comment), "requests.reviewPublished", "requests.reviewFailed");

  if (state === "loading") return <div className="screen screen--with-nav"><LoadingState title={t("deals.loading")} /><BottomNav /></div>;
  if (!deal || state !== "ready") {
    const title = state === "denied" ? t("deals.deniedTitle") : state === "not-found" ? t("deals.notFoundTitle") : t("deals.errorTitle");
    const subtitle = state === "denied" ? t("deals.deniedSubtitle") : state === "not-found" ? t("deals.notFoundSubtitle") : t("deals.errorSubtitle");
    return <div className="screen screen--with-nav"><ErrorState onRetry={state === "error" ? load : undefined} subtitle={subtitle} title={title} /><BottomNav /></div>;
  }

  const locale = language === "uz" ? "uz-UZ" : "ru-RU";
  const formatDate = (value: string) => new Intl.DateTimeFormat(locale, { day: "numeric", month: "short", year: "numeric" }).format(new Date(value));
  const terms = deal.terms;
  const budget = terms && (terms.budgetFrom != null || terms.budgetTo != null)
    ? terms.budgetFrom != null && terms.budgetTo != null ? `${formatCurrency(terms.budgetFrom)}–${formatCurrency(terms.budgetTo)}` : formatCurrency(terms.budgetFrom ?? terms.budgetTo)
    : null;

  return <div className="screen screen--with-nav deal-details">
    <header className="flex items-center justify-between"><ManagementBackLink ariaLabel={t("nav.requests")} href="#/requests" /><Badge tone={dealStatusTone(deal.status)}>{t(dealStatusLabelKey(deal.status))}</Badge></header>
    <p className="mt-4 text-sm font-semibold text-brand-muted">{t(dealSourceLabelKey(deal.sourceType))}</p>
    <h1 className="mt-1 text-3xl font-extrabold tracking-tight">{terms?.title ?? t("deals.source.collaborationRequest")}</h1>
    <Card className="mt-5"><div className="flex items-center gap-3"><Avatar name={deal.counterpartyName} size="sm" src={deal.counterpartyImageUrl} /><div className="min-w-0"><p className="text-xs font-semibold text-brand-muted">{t("deals.counterparty")}</p><p className="truncate font-extrabold">{deal.counterpartyName}</p></div></div></Card>
    {deal.termsSource === "liveCampaignFallback" && <p className="mt-4 text-sm leading-6 text-brand-muted">{t("deals.termsFallbackNote")}</p>}
    {deal.termsSource === "collaboration" && <p className="mt-4 text-sm leading-6 text-brand-muted">{t("deals.collaborationNote")}</p>}
    {terms && <>
      <Card className="mt-4"><h2 className="font-extrabold">{t("deals.terms")}</h2><p className="mt-3 text-sm leading-6 text-brand-muted">{terms.description}</p>
        <dl className="my-campaign-details__facts"><div><dt>{t("common.city")}</dt><dd>{terms.city ? cityLabel(terms.city, language) : t("common.notSpecified")}</dd></div>{budget && <div><dt>{t("deals.campaignBudget")}</dt><dd>{budget}</dd></div>}{terms.deadline && <div><dt>{t("deals.deadline")}</dt><dd>{formatDate(terms.deadline)}</dd></div>}</dl>
        {terms.categories.length > 0 && <div className="mt-4 flex flex-wrap gap-2">{terms.categories.map((category) => <Badge key={category} tone="blue">{categoryLabel(category, language)}</Badge>)}</div>}
      </Card>
      {terms.requirements.length > 0 && <Card className="mt-4"><h2 className="font-extrabold">{t("common.requirements")}</h2><ul className="mt-3 grid gap-2 text-sm text-brand-muted">{terms.requirements.map((value) => <li key={value}>{value}</li>)}</ul></Card>}
    </>}
    <Card className="mt-4"><dl className="my-campaign-details__facts"><div><dt>{t("deals.startedAt")}</dt><dd>{formatDate(deal.createdAtUtc)}</dd></div>{deal.completedAtUtc && <div><dt>{t("deals.completedAt")}</dt><dd>{formatDate(deal.completedAtUtc)}</dd></div>}</dl></Card>
    {deal.canComplete && <Button className="mt-5 w-full" disabled={busy} onClick={() => setCompleteOpen(true)} type="button">{t("requests.complete")}</Button>}
    {deal.canReview && <Card className="mt-5"><h2 className="font-extrabold">{t("requests.reviewTitle")}</h2>
      <div className="mt-3 flex gap-1">{[1, 2, 3, 4, 5].map((value) => <button aria-label={`${t("requests.rating")} ${value}`} aria-pressed={value === rating} className={`grid h-10 w-10 place-items-center rounded-xl text-xl ${value <= rating ? "bg-amber-50 text-amber-500" : "bg-slate-100 text-slate-300"}`} key={value} onClick={() => setRating(value)} type="button">★</button>)}</div>
      <Textarea className="mt-3" maxLength={1000} onChange={(event) => setComment(event.target.value)} placeholder={t("requests.reviewPlaceholder")} value={comment} />
      <Button className="mt-3 w-full" disabled={busy} onClick={review} type="button">{t("requests.publishReview")}</Button>
    </Card>}
    {deal.hasReviewed && <p className="mt-5 text-sm font-semibold text-brand-muted">{t("deals.reviewSent")}</p>}
    <Modal onClose={() => !busy && setCompleteOpen(false)} open={completeOpen} title={t("deals.completeTitle")}><p className="text-sm leading-6 text-brand-muted">{t("deals.completeDescription")}</p><div className="mt-5 grid grid-cols-2 gap-3"><Button disabled={busy} onClick={() => setCompleteOpen(false)} type="button" variant="secondary">{t("common.cancel")}</Button><Button disabled={busy} onClick={complete} type="button">{busy ? t("deals.completing") : t("requests.complete")}</Button></div></Modal>
    <Toast message={toast} tone={tone} />
    <BottomNav />
  </div>;
}
