import { useCallback, useEffect, useRef, useState } from "react";
import { ApiError, getApiErrorMessage } from "../api/client";
import { acceptOffer, declineOffer, getMyOffer, type Offer } from "../api/marketplace";
import { Avatar, Badge, BottomNav, Button, Card, ErrorState, LoadingState, Toast } from "../components/ui";
import { useI18n } from "../i18n";
import { formatCurrency } from "../lib/currency";
import { dealRoute } from "../lib/dealStatus";
import { offerFormatLabelKey, offerStateLabelKey, offerStateTone } from "../lib/offerStatus";
import { PageHeader } from "../components/PageHeader";

type LoadState = "loading" | "ready" | "not-found" | "error";

export function OfferDetails({ id }: { id: string }) {
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
    return <div className="screen screen--with-nav"><ErrorState onRetry={state === "error" ? load : undefined} subtitle={state === "not-found" ? t("offers.notFoundSubtitle") : t("offers.errorSubtitle")} title={state === "not-found" ? t("offers.notFoundTitle") : t("offers.errorTitle")} /><BottomNav /></div>;
  }

  const locale = language === "uz" ? "uz-UZ" : "ru-RU";
  const formatDate = (value: string, withTime = false) => new Intl.DateTimeFormat(locale, withTime ? { day: "numeric", month: "short", hour: "2-digit", minute: "2-digit" } : { day: "numeric", month: "short", year: "numeric" }).format(new Date(value));

  return <div className="screen screen--with-nav offer-details">
    <PageHeader actions={<Badge tone={offerStateTone(offer.state)}>{t(offerStateLabelKey(offer.state))}</Badge>} back={{ href: "#/requests", label: t("nav.requests") }} />
    <p className="mt-4 text-sm font-semibold text-brand-muted">{t("offers.title")}</p>
    <h1 className="mt-1 text-2xl font-extrabold leading-tight">{t(offerFormatLabelKey(offer.format))}</h1>
    <Card className="mt-5"><div className="flex items-center gap-3"><Avatar name={offer.counterpartyName} size="sm" src={offer.counterpartyImageUrl} /><div className="min-w-0"><p className="text-xs font-semibold text-brand-muted">{t("deals.counterparty")}</p><p className="truncate font-extrabold">{offer.counterpartyName}</p></div></div></Card>
    <Card className="mt-4"><p className="whitespace-pre-line text-sm leading-6 text-brand-muted">{offer.message}</p>
      <dl className="my-campaign-details__facts"><div><dt>{t("offers.budget")}</dt><dd>{offer.offeredBudget != null ? formatCurrency(offer.offeredBudget) : t("offers.budgetNegotiable")}</dd></div>{offer.deadline && <div><dt>{t("offers.deadline")}</dt><dd>{formatDate(offer.deadline)}</dd></div>}{offer.state === "pending" && offer.expiresAtUtc && <div><dt>{t("offers.expiresAt")}</dt><dd>{formatDate(offer.expiresAtUtc, true)}</dd></div>}</dl>
    </Card>
    {offer.canRespond && <div className="mt-5 grid grid-cols-2 gap-3"><Button disabled={busy} onClick={() => decide(false)} type="button" variant="secondary">{t("offers.decline")}</Button><Button disabled={busy} onClick={() => decide(true)} type="button">{t("offers.accept")}</Button></div>}
    {offer.dealId && <Button className="mt-5 w-full" onClick={() => { window.location.hash = dealRoute(offer.dealId!); }} type="button">{t("deals.open")}</Button>}
    <Toast message={toast} tone={tone} />
    <BottomNav />
  </div>;
}
