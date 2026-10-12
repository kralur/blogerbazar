import { useCallback, useEffect, useRef, useState } from "react";
import { ApiError } from "../api/client";
import { getAdminFunnel, type AdminFunnel as Funnel, type AdminFunnelPeriod, type AdminFunnelRole } from "../api/marketplace";
import { PageHeader } from "../components/PageHeader";
import { Badge, BottomNav, Card, ErrorState, LoadingState } from "../components/ui";
import { useI18n } from "../i18n";

const periods: AdminFunnelPeriod[] = ["7d", "30d", "all"];
const roles: AdminFunnelRole[] = ["blogger", "brandFace", "business"];

export const adminFunnelHref = "#/admin/funnel";

// Rounded down, so a step that lost even one person never shows 100%.
const percent = (count: number, of: number) => of > 0 ? Math.floor((count / of) * 100) : 0;

// Admin funnel (#/admin/funnel): people who first opened the app in the period and how far each role got, from the
// first open to a review. The step that keeps the smallest share of the previous one is marked: look there first.
export function AdminFunnel() {
  const { t } = useI18n();
  const [period, setPeriod] = useState<AdminFunnelPeriod>("30d");
  const [role, setRole] = useState<AdminFunnelRole>("blogger");
  const [funnel, setFunnel] = useState<Funnel | null>(null);
  const [state, setState] = useState<"loading" | "ready" | "denied" | "failed">("loading");
  // Switching the period quickly must not let an older answer replace a newer one.
  const latestRequest = useRef(0);

  const load = useCallback(() => {
    const request = ++latestRequest.current;
    setState("loading");
    getAdminFunnel(period)
      .then((result) => { if (request === latestRequest.current) { setFunnel(result); setState("ready"); } })
      .catch((error) => { if (request === latestRequest.current) setState(error instanceof ApiError && (error.status === 401 || error.status === 403) ? "denied" : "failed"); });
  }, [period]);
  useEffect(load, [load]);

  const header = <PageHeader back={{ href: "#/admin", label: t("admin.dashboardTitle") }} title={t("admin.funnelTitle")} />;
  if (state === "denied") return <div className="screen screen--with-nav space-y-4 pt-5">{header}<ErrorState onRetry={load} subtitle={t("ui.accessDeniedSubtitle")} title={t("ui.accessDenied")} /><BottomNav /></div>;

  const steps = funnel?.roles.find((item) => item.role === role)?.steps ?? [];
  const opened = steps[0]?.count ?? 0;
  let weakest = -1;
  let weakestRate = 100;
  steps.forEach((step, index) => {
    const previous = index > 0 ? steps[index - 1].count : 0;
    if (previous > 0 && percent(step.count, previous) < weakestRate) { weakestRate = percent(step.count, previous); weakest = index; }
  });

  return <div className="screen screen--with-nav space-y-4 pt-5">
    {header}
    <p className="text-sm leading-6 text-brand-muted">{t("admin.funnelHint")}</p>
    <div aria-label={t("admin.funnelPeriodLabel")} className="admin-filters no-scrollbar" role="group">
      {periods.map((value) => <button aria-pressed={period === value} className="admin-filter" key={value} onClick={() => setPeriod(value)} type="button">{t(`admin.funnelPeriod.${value}`)}</button>)}
    </div>
    <div aria-label={t("admin.funnelRoleLabel")} className="admin-filters no-scrollbar" role="group">
      {roles.map((value) => <button aria-pressed={role === value} className="admin-filter" key={value} onClick={() => setRole(value)} type="button">{t(`admin.funnelRole.${value}`)}</button>)}
    </div>
    {state === "failed" ? <ErrorState onRetry={load} subtitle={t("common.connectionRetry")} title={t("common.openFailed")} />
      : state === "loading" || !funnel ? <LoadingState />
      : opened === 0 ? <Card><p className="text-sm text-brand-muted">{t("admin.funnelEmpty")}</p></Card>
      : <Card className="p-4"><ol className="admin-funnel">
        {steps.map((step, index) => <li key={step.key}>
          <div className="admin-funnel__head"><span className="admin-funnel__label">{t(`admin.funnelStep.${step.key}`)}</span><strong className="admin-funnel__count">{step.count}</strong></div>
          <div aria-hidden="true" className="admin-funnel__bar"><span style={{ width: `${percent(step.count, opened)}%` }} /></div>
          {index > 0 && <p className="admin-funnel__rates">{steps[index - 1].count > 0
            ? t("admin.funnelRates", { share: percent(step.count, opened), step: percent(step.count, steps[index - 1].count) })
            : t("admin.funnelShare", { share: percent(step.count, opened) })}</p>}
          {index === weakest && <div className="mt-1"><Badge tone="orange">{t("admin.funnelBiggestDrop")}</Badge></div>}
        </li>)}
      </ol></Card>}
    <BottomNav />
  </div>;
}
