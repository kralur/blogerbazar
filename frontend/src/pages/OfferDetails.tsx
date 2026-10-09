import { useCallback, useEffect, useRef, useState } from "react";
import { ApiError, getApiErrorMessage } from "../api/client";
import { acceptOffer, declineOffer, getMyOffer, type MarketplaceRole, type Offer } from "../api/marketplace";
import { Avatar, Badge, BottomNav, Button, Card, ErrorState, LoadingState, Toast } from "../components/ui";
import { useI18n } from "../i18n";
import { formatShortDate, formatCurrency } from "../lib/currency";
import { dealRoute } from "../lib/dealStatus";
import { offerFormatLabelKey, offerStateLabelKey, offerStateTone } from "../lib/offerStatus";
import { profileRoute } from "../lib/profileRoutes";
import { SwitchRoleHint } from "../components/SwitchRoleHint";
import { PageHeader } from "../components/PageHeader";
import { DetailSection, FactGrid } from "../components/details/DetailBlocks";
import { useScreenRefresh } from "../hooks/useScreenRefresh";
import { CounterpartyRow } from "../components/CounterpartyRow";

type LoadState = "loading" | "ready" | "not-found" | "error";

export function OfferDetails({ id, viewerRole }: { id: string; viewerRole?: MarketplaceRole }) {
  const { language, t } = useI18n();
  const [offer, setOffer] = useState<Offer | null>(null);
  const [state, setState] = useState<LoadState>("loading");
  const [busy, setBusy] = useState(false);
  const [toast, setToast] = useState("");
  const [tone, setTone] = useState<"success" | "error">("success");
  const requestRef = useRef(0);
  const busyRef = useRef(false);
  const idRef = useRef(id);
  idRef.current = id;

  const load = useCallback(async () => {
    const requestId = ++requestRef.current;
    try {
      const value = await getMyOffer(id);
      if (requestId !== requestRef.current || idRef.current !== id) return;
      setOffer(value);
      setState("ready");
    } catch (error) {
      if (requestId !== requestRef.current || idRef.current !== id) return;
      setState(error instanceof ApiError && (error.status === 404 || error.status === 403) ? "not-found" : "error");
    }
  }, [id]);

  useEffect(() => {
    setState("loading");
    setOffer(null);
    void load();
    return () => { requestRef.current += 1; busyRef.current = false; };
  }, [load]);
  useScreenRefresh(load);

  const decide = async (accept: boolean) => {
    if (busyRef.current) return;
    busyRef.current = true;
    setBusy(true);
    try {
      const decision = accept ? await acceptOffer(id) : await declineOffer(id);
      if (idRef.current !== id) return;
      if (accept && decision.dealId) {
        window.location.hash = dealRoute(decision.dealId);
        return;
      }
      setTone("success");
      setToast(t(accept ? "offers.acceptedToast" : "offers.declinedToast"));
      await load();
    } catch (error) {
      if (idRef.current !== id) return;
      if (error instanceof ApiError && error.status === 409) {
        setTone("error");
        setToast(t("offers.noLongerPending"));
        await load();
      } else if (error instanceof ApiError && error.status === 404) setState("not-found");
      else { setTone("error"); setToast(getApiErrorMessage(error, t("offers.decisionFailed"))); }
    } finally {
      if (idRef.current === id) { busyRef.current = false; setBusy(false); }
    }
  };

  if (state === "loading") return <div className="screen screen--with-nav"><LoadingState title={t("offers.loading")} /><BottomNav /></div>;
  if (!offer || state !== "ready") {
    return <div className="screen screen--with-nav"><ErrorState onRetry={state === "error" ? load : undefined} subtitle={state === "not-found" ? t("offers.notFoundSubtitle") : t("offers.errorSubtitle")} title={state === "not-found" ? t("offers.notFoundTitle") : t("offers.errorTitle")} />{state === "not-found" && <SwitchRoleHint />}<BottomNav /></div>;
  }

  const formatDate = (value: string, withTime = false) => formatShortDate(value, language, withTime ? { time: true } : { year: true });

  return <div className="screen screen--with-nav offer-details">
    <PageHeader actions={<Badge tone={offerStateTone(offer.state)}>{t(offerStateLabelKey(offer.state))}</Badge>} back={{ href: "#/requests", label: t("nav.requests") }} />
    <p className="mt-4 text-sm font-semibold text-brand-muted">{t("offers.title")}</p>
    <h1 className="mt-1 text-2xl font-extrabold leading-tight tracking-tight">{t(offerFormatLabelKey(offer.format))}</h1>
    <CounterpartyRow href={viewerRole === "Business" ? (offer.brandFaceId ? profileRoute("brandFace", offer.brandFaceId) : offer.bloggerId ? profileRoute("blogger", offer.bloggerId) : null) : (viewerRole === "Blogger" || viewerRole === "BrandFace") && offer.businessId ? `#/company/${offer.businessId}` : null} imageUrl={offer.counterpartyImageUrl} label={t(viewerRole === "Business" ? (offer.brandFaceId ? "offers.counterpartyBrandFace" : "offers.counterpartyBlogger") : viewerRole === "Blogger" || viewerRole === "BrandFace" ? "offers.counterpartyBusiness" : "offers.counterparty")} name={offer.counterpartyName} />
    <FactGrid className="mt-4" facts={[
      { label: t("offers.budget"), value: offer.offeredBudget != null ? formatCurrency(offer.offeredBudget) : t("offers.budgetNegotiable") },
      { label: t("offers.deadline"), value: offer.deadline ? formatDate(offer.deadline) : null },
      { label: t("offers.expiresAt"), value: offer.state === "pending" && offer.expiresAtUtc ? formatDate(offer.expiresAtUtc, true) : null, wide: true }
    ]} />
    <DetailSection title={t("offers.message")}><Card><p className="whitespace-pre-line text-sm leading-6 text-brand-muted">{offer.message}</p></Card></DetailSection>
    {offer.canRespond && <div className="offer-respond mt-5">
      {offer.expiresAtUtc && <OfferTimeLeft expiresAtUtc={offer.expiresAtUtc} />}
      <p className="offer-respond__hint">{t("offers.acceptHint")}</p>
      <div className="grid grid-cols-2 gap-3"><Button disabled={busy} onClick={() => decide(false)} type="button" variant="secondary">{t("offers.decline")}</Button><Button disabled={busy} onClick={() => decide(true)} type="button">{t("offers.accept")}</Button></div>
    </div>}
    {offer.dealId && <Button className="mt-5 w-full" onClick={() => { window.location.hash = dealRoute(offer.dealId!); }} type="button">{t("deals.open")}</Button>}
    <Toast message={toast} tone={tone} />
    <BottomNav />
  </div>;
}

// Time to answer, rounded down: days while a day or more remains, then hours; urgent under 12 hours.
function OfferTimeLeft({ expiresAtUtc }: { expiresAtUtc: string }) {
  const { t } = useI18n();
  const msLeft = new Date(expiresAtUtc).getTime() - Date.now();
  if (!(msLeft > 0)) return null;
  const hours = Math.floor(msLeft / 3_600_000);
  const text = hours < 1 ? t("offers.timeLeftUnderHour") : hours < 24 ? t("offers.timeLeftHours", { count: hours }) : t("offers.timeLeftDays", { count: Math.floor(hours / 24) });
  return <p className={`offer-respond__left${hours < 12 ? " offer-respond__left--urgent" : ""}`} role="status">{text}</p>;
}
