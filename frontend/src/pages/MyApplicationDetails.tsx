import { useCallback, useEffect, useRef, useState } from "react";
import { ApiError } from "../api/client";
import { getApiErrorMessage } from "../api/client";
import { getMyCampaignApplication, withdrawMyCampaignApplication, type MyCampaignApplicationDetails as Application } from "../api/marketplace";
import { getCachedCampaignApplicationDetails, setCachedCampaignApplicationDetails, updateCachedCampaignApplication } from "../data/campaignApplicationCache";
import { Badge, BottomNav, Button, Card, ErrorState, LoadingState, Modal, Toast } from "../components/ui";
import { useI18n } from "../i18n";
import { formatCurrency } from "../lib/currency";
import { campaignApplicationStatusLabelKey, campaignApplicationStatusTone, canWithdrawCampaignApplication } from "../lib/campaignApplicationStatus";
import { ManagementBackLink } from "../components/ManagementBackLink";

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
  const budget = application.minBudget != null && application.maxBudget != null ? `${formatCurrency(application.minBudget)}–${formatCurrency(application.maxBudget)}` : application.minBudget != null ? formatCurrency(application.minBudget) : application.maxBudget != null ? formatCurrency(application.maxBudget) : null;
  return <div className="screen screen--with-nav application-details"><header className="flex items-center justify-between"><ManagementBackLink ariaLabel={t("nav.requests")} href="#/requests" /><Badge tone={campaignApplicationStatusTone(application.status)}>{t(campaignApplicationStatusLabelKey(application.status))}</Badge></header><h1 className="mt-4 text-3xl font-extrabold tracking-tight">{application.campaignTitle}</h1><p className="mt-2 text-sm text-brand-muted">{application.businessName}</p><Card className="mt-5"><h2 className="font-extrabold">{t("myCampaignDetails.description")}</h2><p className="mt-3 text-sm leading-6 text-brand-muted">{application.campaignDescription}</p></Card><Card className="mt-4"><dl className="my-campaign-details__facts"><div><dt>{t("common.city")}</dt><dd>{application.city ?? t("common.notSpecified")}</dd></div>{budget && <div><dt>{t("common.budget")}</dt><dd>{budget}</dd></div>}<div><dt>{t("common.date")}</dt><dd>{new Intl.DateTimeFormat(locale, { day: "numeric", month: "short" }).format(new Date(application.createdAtUtc))}</dd></div></dl></Card><Card className="mt-4"><h2 className="font-extrabold">{t("common.requirements")}</h2><ul className="mt-3 grid gap-2 text-sm text-brand-muted">{application.requirements.map((value) => <li key={value}>{value}</li>)}</ul><p className="mt-4 text-sm text-brand-muted">{application.message ?? t("applications.noMessage")}</p></Card>{canWithdrawCampaignApplication(application.status) && <Button className="mt-5 w-full" onClick={() => setWithdrawOpen(true)} type="button" variant="danger">{t("applications.withdraw")}</Button>}<Modal onClose={() => !withdrawing && !reconciling && setWithdrawOpen(false)} open={withdrawOpen} title={t("applications.withdrawTitle")}><p className="text-sm leading-6 text-brand-muted">{t("applications.withdrawDescription")}</p><div className="mt-5 grid grid-cols-2 gap-3"><Button disabled={withdrawing || reconciling} onClick={() => setWithdrawOpen(false)} type="button" variant="secondary">{t("common.cancel")}</Button><Button disabled={withdrawing || reconciling} onClick={withdraw} type="button" variant="danger">{withdrawing || reconciling ? t("applications.withdrawing") : t("applications.withdraw")}</Button></div></Modal><Toast message={toast} tone={tone} /><BottomNav /></div>;
}
