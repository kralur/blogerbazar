import { useCallback, useEffect, useRef, useState } from "react";
import { ApiError } from "../api/client";
import { getApiErrorMessage } from "../api/client";
import { getMyCampaignApplication, withdrawMyCampaignApplication, type MyCampaignApplicationDetails as Application } from "../api/marketplace";
import { getCachedCampaignApplicationDetails, setCachedCampaignApplicationDetails, updateCachedCampaignApplication } from "../data/campaignApplicationCache";
import { Badge, BottomNav, Button, Card, ErrorState, LoadingState, Modal, Toast } from "../components/ui";
import { cityLabel, useI18n } from "../i18n";
import { formatBudgetRange } from "../lib/currency";
import { campaignApplicationStatusLabelKey, campaignApplicationStatusTone, canWithdrawCampaignApplication } from "../lib/campaignApplicationStatus";
import { dealRoute } from "../lib/dealStatus";
import { PageHeader } from "../components/PageHeader";
import { DetailSection, FactGrid } from "../components/details/DetailBlocks";

export function MyApplicationDetails({ id }: { id: string }) {
  const { language, t } = useI18n();
  const [application, setApplication] = useState<Application | null>(() => getCachedCampaignApplicationDetails(id));
  const [state, setState] = useState<"loading" | "ready" | "denied" | "not-found" | "error">(application ? "ready" : "loading");
  const [withdrawOpen, setWithdrawOpen] = useState(false);
  const [withdrawing, setWithdrawing] = useState(false);
  const [reconciling, setReconciling] = useState(false);
  const [toast, setToast] = useState("");
  const [tone, setTone] = useState<"success" | "error">("success");
  const requestRef = useRef(0);
  const idRef = useRef(id);
  idRef.current = id;
  const withdrawRef = useRef(0);
  const withdrawingRef = useRef(false);
  const load = useCallback(async () => {
    const requestId = ++requestRef.current;
    const cached = getCachedCampaignApplicationDetails(id);
    if (!cached) setState("loading");
    try {
      const value = await getMyCampaignApplication(id);
      if (requestId !== requestRef.current || idRef.current !== id) return;
      setCachedCampaignApplicationDetails(value);
      setApplication(value);
      setState("ready");
    } catch (error) {
      if (requestId !== requestRef.current || idRef.current !== id) return;
      const terminalFailure = error instanceof ApiError && error.status === 404 ? "not-found" : error instanceof ApiError && (error.status === 401 || error.status === 403) ? "denied" : null;
      if (terminalFailure) setState(terminalFailure);
      else if (cached) {
        setState("ready");
        setTone("error");
        setToast(t("applications.errorSubtitle"));
      } else setState("error");
    }
  }, [id, t]);
  useEffect(() => { void load(); return () => { requestRef.current += 1; withdrawRef.current += 1; withdrawingRef.current = false; }; }, [load]);
  const withdraw = async () => {
    if (!application || withdrawingRef.current) return;
    withdrawingRef.current = true;
    const applicationId = application.id;
    const requestId = ++withdrawRef.current;
    setWithdrawing(true);
    try {
      const result = await withdrawMyCampaignApplication(applicationId);
      if (requestId !== withdrawRef.current || idRef.current !== id) return;
      updateCachedCampaignApplication(application.id, result.status);
      setApplication((current) => current ? { ...current, status: result.status } : current);
      setWithdrawOpen(false); setTone("success"); setToast(t("applications.withdrawnSuccess"));
    } catch (error) {
      if (requestId !== withdrawRef.current || idRef.current !== id) return;
      if (error instanceof ApiError && error.status === 409) {
        setReconciling(true);
        await load();
        if (requestId === withdrawRef.current && idRef.current === id) setWithdrawOpen(false);
      } else if (error instanceof ApiError && error.status === 404) { setWithdrawOpen(false); setState("not-found"); }
      else if (error instanceof ApiError && (error.status === 401 || error.status === 403)) { setWithdrawOpen(false); setState("denied"); }
      else { setTone("error"); setToast(getApiErrorMessage(error, t("applications.withdrawFailed"))); }
    } finally {
      if (requestId === withdrawRef.current && idRef.current === id) { withdrawingRef.current = false; setWithdrawing(false); setReconciling(false); }
    }
  };
  if (state === "loading") return <div className="screen screen--with-nav"><LoadingState title={t("applications.loading")} /><BottomNav /></div>;
  if (!application || state !== "ready") return <div className="screen screen--with-nav"><ErrorState onRetry={state === "error" ? load : undefined} subtitle={t(state === "denied" ? "applications.deniedSubtitle" : state === "not-found" ? "applications.notFoundSubtitle" : "applications.errorSubtitle")} title={t(state === "denied" ? "applications.deniedTitle" : state === "not-found" ? "applications.notFoundTitle" : "applications.errorTitle")} /><BottomNav /></div>;
  const locale = language === "uz" ? "uz-UZ" : "ru-RU";
  const budget = formatBudgetRange(application.minBudget, application.maxBudget);
  return <div className="screen screen--with-nav application-details"><PageHeader actions={<Badge tone={campaignApplicationStatusTone(application.status)}>{t(campaignApplicationStatusLabelKey(application.status))}</Badge>} back={{ href: "#/requests", label: t("nav.requests") }} /><h1 className="mt-4 text-2xl font-extrabold leading-tight tracking-tight">{application.campaignTitle}</h1><p className="mt-2 text-sm text-brand-muted">{application.businessName}</p><FactGrid className="mt-4" facts={[{ label: t("common.city"), value: application.city ? cityLabel(application.city, language) : t("common.notSpecified") }, { label: t("common.date"), value: new Intl.DateTimeFormat(locale, { day: "numeric", month: "short" }).format(new Date(application.createdAtUtc)) }, { label: t("common.budget"), value: budget, wide: true }]} /><DetailSection title={t("myCampaignDetails.description")}><Card><p className="text-sm leading-6 text-brand-muted">{application.campaignDescription}</p></Card></DetailSection>{application.requirements.length > 0 && <DetailSection title={t("common.requirements")}><Card><ul className="grid gap-2 text-sm text-brand-muted">{application.requirements.map((value) => <li key={value}>{value}</li>)}</ul></Card></DetailSection>}<DetailSection title={t("applications.yourMessage")}><Card><p className="whitespace-pre-line text-sm leading-6 text-brand-muted">{application.message ?? t("applications.noMessage")}</p></Card></DetailSection>{application.dealId && <Button className="mt-5 w-full" onClick={() => { window.location.hash = dealRoute(application.dealId!); }} type="button">{t("deals.open")}</Button>}{canWithdrawCampaignApplication(application.status) && <Button className="mt-5 w-full" onClick={() => setWithdrawOpen(true)} type="button" variant="danger">{t("applications.withdraw")}</Button>}<Modal onClose={() => !withdrawing && !reconciling && setWithdrawOpen(false)} open={withdrawOpen} title={t("applications.withdrawTitle")}><p className="text-sm leading-6 text-brand-muted">{t("applications.withdrawDescription")}</p><div className="mt-5 grid grid-cols-2 gap-3"><Button disabled={withdrawing || reconciling} onClick={() => setWithdrawOpen(false)} type="button" variant="secondary">{t("common.cancel")}</Button><Button disabled={withdrawing || reconciling} onClick={withdraw} type="button" variant="danger">{withdrawing || reconciling ? t("applications.withdrawing") : t("applications.withdraw")}</Button></div></Modal><Toast message={toast} tone={tone} /><BottomNav /></div>;
}
