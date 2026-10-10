import { useCallback, useEffect, useRef, useState } from "react";
import { ApiError, getApiErrorMessage } from "../api/client";
import { addDealPublication, completeDeal, createDealReview, setDealPrice, getDealContact, getMyDeal, shareDealContact, type ContactDetails, type DealDetails as Deal, type MarketplaceRole } from "../api/marketplace";
import { ContactList, hasContacts } from "../components/ContactList";
import { getCachedDeal, setCachedDeal } from "../data/dealCache";
import { Avatar, Badge, BottomNav, Button, Card, ErrorState, Input, LoadingState, Modal, Textarea, Toast } from "../components/ui";
import { DealResults, digitsToNumber, isHttpsLink } from "../components/DealResults";
import { categoryLabel, cityLabel, useI18n } from "../i18n";
import { formatShortDate, formatBudgetRange, formatCurrency, formatNumericInput } from "../lib/currency";
import { DealStatus, dealSourceLabelKey, dealStatusLabelKey, dealStatusTone } from "../lib/dealStatus";
import { offerFormatLabelKey } from "../lib/offerStatus";
import { profileRoute } from "../lib/profileRoutes";
import { markDealSeen } from "../data/seenDeals";
import { BlessingButton } from "../components/BlessingButton";
import { SwitchRoleHint } from "../components/SwitchRoleHint";
import { PageHeader } from "../components/PageHeader";
import { ChipList, DetailSection, FactGrid } from "../components/details/DetailBlocks";
import { useScreenRefresh } from "../hooks/useScreenRefresh";
import { CounterpartyRow } from "../components/CounterpartyRow";

type LoadState = "loading" | "ready" | "denied" | "not-found" | "error";

export function DealDetails({ id, viewerRole }: { id: string; viewerRole?: MarketplaceRole }) {
  const { language, t } = useI18n();
  const [deal, setDeal] = useState<Deal | null>(() => getCachedDeal(id));
  const [state, setState] = useState<LoadState>(deal ? "ready" : "loading");
  // Opening the deal once clears its "new" dot in Requests.
  useEffect(() => { if (deal) markDealSeen(deal.id); }, [deal?.id]);
  const [completeOpen, setCompleteOpen] = useState(false);
  const [busy, setBusy] = useState(false);
  const [rating, setRating] = useState(5);
  const [comment, setComment] = useState("");
  // Optional results asked right at completion, where both sides already are (D51).
  const [completeLink, setCompleteLink] = useState("");
  const [completePrice, setCompletePrice] = useState("");
  const [completeError, setCompleteError] = useState("");
  const [contact, setContact] = useState<ContactDetails | null>(null);
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
  useScreenRefresh(load);

  const ready = state === "ready";
  useEffect(() => {
    if (!ready) return;
    let active = true;
    setContact(null);
    getDealContact(id).then((value) => { if (active) setContact(value); }).catch(() => undefined);
    return () => { active = false; };
  }, [id, ready]);

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

  const askLink = Boolean(deal?.canAddPublication && (deal.publications?.length ?? 0) === 0);
  const askPrice = Boolean(deal?.canSetPrice && deal.agreedPrice == null);
  const openComplete = () => { setCompleteLink(""); setCompletePrice(""); setCompleteError(""); setCompleteOpen(true); };
  const complete = () => {
    const link = askLink ? completeLink.trim() : "";
    const price = askPrice ? digitsToNumber(completePrice) : null;
    if (link && !isHttpsLink(link)) { setCompleteError(t("deals.publicationLinkInvalid")); return; }
    return runAction(async () => {
      // A retry after a failed completion finds the link already saved; that is fine, go on to complete.
      if (link) await addDealPublication(id, link).catch((error) => { if (!(error instanceof ApiError && error.code === "publication_duplicate")) throw error; });
      if (price != null) await setDealPrice(id, price);
      await completeDeal(id);
    }, "requests.completedToast", "requests.completeFailed");
  };
  const review = () => runAction(() => createDealReview(id, rating, comment), "requests.reviewPublished", "requests.reviewFailed");

  if (state === "loading") return <div className="screen screen--with-nav"><LoadingState title={t("deals.loading")} /><BottomNav /></div>;
  if (!deal || state !== "ready") {
    const title = state === "denied" ? t("deals.deniedTitle") : state === "not-found" ? t("deals.notFoundTitle") : t("deals.errorTitle");
    const subtitle = state === "denied" ? t("deals.deniedSubtitle") : state === "not-found" ? t("deals.notFoundSubtitle") : t("deals.errorSubtitle");
    return <div className="screen screen--with-nav"><ErrorState onRetry={state === "error" ? load : undefined} subtitle={subtitle} title={title} />{(state === "not-found" || state === "denied") && <SwitchRoleHint />}<BottomNav /></div>;
  }

  const formatDate = (value: string) => formatShortDate(value, language, { year: true });
  const contactItems = [
    contact?.telegram ? { kind: "telegram" as const, value: contact.telegram } : null,
    contact?.phone ? { kind: "phone" as const, value: contact.phone } : null,
    contact?.email ? { kind: "email" as const, value: contact.email } : null,
    contact?.websiteUrl ? { kind: "website" as const, value: contact.websiteUrl } : null
  ].filter((item): item is NonNullable<typeof item> => item !== null);
  const terms = deal.terms;
  const budget = terms ? formatBudgetRange(terms.budgetFrom, terms.budgetTo) : null;

  return <div className="screen screen--with-nav deal-details">
    <PageHeader actions={<Badge tone={dealStatusTone(deal.status)}>{t(dealStatusLabelKey(deal.status))}</Badge>} back={{ href: "#/requests", label: t("nav.requests") }} />
    <p className="mt-4 text-sm font-semibold text-brand-muted">{t(dealSourceLabelKey(deal.sourceType))}</p>
    <h1 className="mt-1 text-2xl font-extrabold leading-tight tracking-tight">{terms?.title ?? (deal.offer ? t(offerFormatLabelKey(deal.offer.format)) : t("deals.source.collaborationRequest"))}</h1>
    <CounterpartyRow href={deal.counterpartyProfileId ? (deal.counterpartyRole ? profileRoute(deal.counterpartyRole, deal.counterpartyProfileId) : viewerRole === "Business" ? `#/blogger/${deal.counterpartyProfileId}` : viewerRole === "Blogger" || viewerRole === "BrandFace" ? `#/company/${deal.counterpartyProfileId}` : null) : null} imageUrl={deal.counterpartyImageUrl} label={t("deals.counterparty")} name={deal.counterpartyDeleted ? t("common.deletedAccount") : deal.counterpartyName} />
    {deal.status === DealStatus.Active && !deal.counterpartyDeleted && <BlessingButton />}
    {deal.counterpartyDeleted && <p className="mt-3 text-sm leading-6 text-brand-muted" role="status">{t("deals.partnerDeletedNote")}</p>}
    {deal.termsSource === "liveCampaignFallback" && <p className="mt-4 text-sm leading-6 text-brand-muted">{t("deals.termsFallbackNote")}</p>}
    {deal.termsSource === "collaboration" && <p className="mt-4 text-sm leading-6 text-brand-muted">{t("deals.collaborationNote")}</p>}
    {terms && <DetailSection title={t("deals.terms")}>
      <FactGrid facts={[
        { label: t("common.city"), value: terms.city ? cityLabel(terms.city, language) : t("common.notSpecified") },
        { label: t("deals.deadline"), value: terms.deadline ? formatDate(terms.deadline) : null },
        { label: t("deals.campaignBudget"), value: budget, wide: true }
      ]} />
      {terms.description && <Card className="mt-2"><p className="text-sm leading-6 text-brand-muted">{terms.description}</p></Card>}
      {terms.categories.length > 0 && <div className="mt-3"><ChipList items={terms.categories.map((category) => categoryLabel(category, language))} /></div>}
    </DetailSection>}
    {terms && terms.requirements.length > 0 && <DetailSection title={t("common.requirements")}><Card><ul className="grid gap-2 text-sm text-brand-muted">{terms.requirements.map((value) => <li key={value}>{value}</li>)}</ul></Card></DetailSection>}
    {deal.offer && <DetailSection title={t("deals.offerTerms")}>
      <FactGrid facts={[
        { label: t("offers.format"), value: t(offerFormatLabelKey(deal.offer.format)) },
        { label: t("offers.budget"), value: deal.offer.offeredBudget != null ? formatCurrency(deal.offer.offeredBudget) : t("offers.budgetNegotiable") },
        { label: t("offers.deadline"), value: deal.offer.deadline ? formatDate(deal.offer.deadline) : null }
      ]} />
      <Card className="mt-2"><p className="whitespace-pre-line text-sm leading-6 text-brand-muted">{deal.offer.message}</p></Card>
    </DetailSection>}
    {contactItems.length > 0 && hasContacts(contactItems) && <DetailSection title={t("deals.contacts")}><ContactList items={contactItems} onSendToChat={async () => { try { await shareDealContact(deal.id); return t("contacts.sentToChat"); } catch (error) { throw new Error(getApiErrorMessage(error, t("contacts.sendToChatFailed"))); } }} /></DetailSection>}
    <FactGrid className="mt-4" facts={[
      { label: t("deals.startedAt"), value: formatDate(deal.createdAtUtc) },
      { label: t("deals.completedAt"), value: deal.completedAtUtc ? formatDate(deal.completedAtUtc) : null }
    ]} />
    <DealResults deal={deal} onChanged={load} onMessage={(message, nextTone) => { setTone(nextTone); setToast(message); }} />
    {deal.canComplete && <Button className="mt-5 w-full" disabled={busy} onClick={openComplete} type="button">{t("requests.complete")}</Button>}
    {deal.canReview && <Card className="mt-5"><h2 className="font-extrabold">{t("requests.reviewTitle")}</h2>
      {deal.partnerHasReviewed && <p className="deal-review__partner" role="status"><span aria-hidden="true" className="request-row__action-dot" />{t("deals.partnerReviewedHint", { partner: deal.counterpartyName })}</p>}
      <div className="mt-3 flex gap-1">{[1, 2, 3, 4, 5].map((value) => <button aria-label={`${t("requests.rating")} ${value}`} aria-pressed={value === rating} className={`grid h-10 w-10 place-items-center rounded-xl text-xl ${value <= rating ? "bg-brand-soft text-brand-warning" : "bg-brand-soft text-brand-muted opacity-50"}`} key={value} onClick={() => setRating(value)} type="button">★</button>)}</div>
      <Textarea className="mt-3" maxLength={1000} onChange={(event) => setComment(event.target.value)} placeholder={t("requests.reviewPlaceholder")} value={comment} />
      {!deal.partnerHasReviewed && <p className="mt-3 text-xs leading-5 text-brand-muted">{t("deals.reviewBlindHint")}</p>}
      {deal.reviewDeadlineUtc && <p className="mt-1 text-xs font-semibold text-brand-muted">{t("deals.reviewDeadline", { date: formatDate(deal.reviewDeadlineUtc) })}</p>}
      <Button className="mt-3 w-full" disabled={busy} onClick={review} type="button">{t("requests.publishReview")}</Button>
    </Card>}
    {deal.hasReviewed && <p className="mt-5 text-sm font-semibold text-brand-muted">{t("deals.reviewSent")}</p>}
    {deal.status === DealStatus.Completed && !deal.canReview && !deal.hasReviewed && deal.reviewDeadlineUtc && <p className="mt-5 text-sm font-semibold text-brand-muted">{t("deals.reviewClosed")}</p>}
    <Modal onClose={() => !busy && setCompleteOpen(false)} open={completeOpen} title={t("deals.completeTitle")}><p className="text-sm leading-6 text-brand-muted">{t("deals.completeDescription")}</p>
      {(askLink || askPrice) && <div className="mt-4 grid gap-3">
        {askLink && <Input inputMode="url" label={t("deals.completeLinkLabel")} onChange={(event) => { setCompleteLink(event.target.value); setCompleteError(""); }} placeholder="https://" type="url" value={completeLink} />}
        {askPrice && <Input inputMode="numeric" label={t("deals.completePriceLabel")} onChange={(event) => setCompletePrice(formatNumericInput(event.target.value))} suffix={t("currency.uzs")} value={completePrice} />}
        <p className="text-xs leading-5 text-brand-muted">{t("deals.completeResultsHint")}</p>
        {completeError && <p className="text-sm font-semibold text-brand-danger" role="alert">{completeError}</p>}
      </div>}
      <div className="mt-5 grid grid-cols-2 gap-3"><Button disabled={busy} onClick={() => setCompleteOpen(false)} type="button" variant="secondary">{t("common.cancel")}</Button><Button disabled={busy} onClick={complete} type="button">{busy ? t("deals.completing") : t("requests.complete")}</Button></div></Modal>
    <Toast message={toast} tone={tone} />
    <BottomNav />
  </div>;
}
