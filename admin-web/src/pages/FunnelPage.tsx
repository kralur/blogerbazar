import { useState } from "react";
import { getFunnel, type FunnelPeriod, type FunnelRole } from "../api/analytics";
import { ExportButton } from "../components/ExportButton";
import { Report } from "../components/Report";
import { Badge, Choice, Table } from "../components/ui";
import { useI18n } from "../i18n";
import { formatNumber } from "../lib/format";
import { useLoad } from "../lib/useLoad";

const periods: FunnelPeriod[] = ["7d", "30d", "all"];
const roles: FunnelRole[] = ["blogger", "brandFace", "business"];
const share = (part: number, whole: number) => (whole > 0 ? Math.round((part * 100) / whole) : 0);

// The step after which the largest share of people stopped; -1 while no step has anyone before it.
export function biggestDrop(counts: number[]) {
  let weakest = -1;
  let weakestRate = 101;
  counts.forEach((count, index) => {
    const previous = index > 0 ? counts[index - 1] : 0;
    if (previous > 0 && share(count, previous) < weakestRate) {
      weakestRate = share(count, previous);
      weakest = index;
    }
  });
  return weakest;
}

// Funnel: people who first opened the app in the period and how far each role got, as bars against the first step.
export function FunnelPage() {
  const { t } = useI18n();
  const [period, setPeriod] = useState<FunnelPeriod>("30d");
  const [role, setRole] = useState<FunnelRole>("blogger");
  const [view, setView] = useState<"chart" | "table">("chart");
  const { state, reload } = useLoad(() => getFunnel(period), period);

  return (
    <div className="space-y-6">
      <div className="space-y-3">
        <Choice label={t("admin.funnelPeriodLabel")} value={period} onChange={setPeriod} options={periods.map((value) => ({ value, label: t(`admin.funnelPeriod.${value}`) }))} />
        <Choice label={t("admin.funnelRoleLabel")} value={role} onChange={setRole} options={roles.map((value) => ({ value, label: t(`admin.funnelRole.${value}`) }))} />
      </div>
      <Report
        title={t(`admin.funnelRole.${role}`)}
        hint={t("admin.funnelHint")}
        loaded={state}
        onRetry={reload}
        actions={<div className="flex flex-wrap gap-2">
          <Choice label={t("report.view")} value={view} onChange={setView} options={[{ value: "chart", label: t("report.viewChart") }, { value: "table", label: t("report.viewTable") }]} />
          {state.status === "ready" && <ExportButton kind="funnel" table={() => ({
            head: [t("admin.funnelRoleLabel"), t("funnel.step"), t("funnel.people"), t("funnel.ofOpened"), t("funnel.ofPrevious")],
            rows: state.data.roles.flatMap((item) => item.steps.map((step, index) => [
              t(`admin.funnelRole.${item.role}`), t(`admin.funnelStep.${step.key}`), step.count,
              item.steps[0].count > 0 ? `${share(step.count, item.steps[0].count)}%` : "",
              index > 0 && item.steps[index - 1].count > 0 ? `${share(step.count, item.steps[index - 1].count)}%` : ""
            ]))
          })} />}
        </div>}
      >
        {(funnel) => {
          const steps = funnel.roles.find((item) => item.role === role)?.steps ?? [];
          const opened = steps[0]?.count ?? 0;
          if (opened === 0) return <p className="text-sm text-brand-muted">{t("admin.funnelEmpty")}</p>;
          const weakest = biggestDrop(steps.map((step) => step.count));
          if (view === "table") {
            return (
              <Table
                caption={t(`admin.funnelRole.${role}`)}
                head={[t("funnel.step"), t("funnel.people"), t("funnel.ofOpened"), t("funnel.ofPrevious")]}
                rows={steps.map((step, index) => [
                  t(`admin.funnelStep.${step.key}`),
                  formatNumber(step.count),
                  `${share(step.count, opened)}%`,
                  index > 0 && steps[index - 1].count > 0 ? `${share(step.count, steps[index - 1].count)}%` : "–"
                ])}
              />
            );
          }
          return (
            <ol className="space-y-4">
              {steps.map((step, index) => (
                <li key={step.key} className="min-w-0">
                  <div className="flex items-baseline justify-between gap-3">
                    <span className="min-w-0 text-sm font-semibold">{t(`admin.funnelStep.${step.key}`)}</span>
                    <strong className="shrink-0 text-base tabular-nums">{formatNumber(step.count)}</strong>
                  </div>
                  <div aria-hidden="true" className="mt-1.5 h-3 overflow-hidden rounded-full bg-brand-soft">
                    <div className="h-full rounded-full bg-brand-blue" style={{ width: `${share(step.count, opened)}%` }} />
                  </div>
                  {index > 0 && (
                    <p className="mt-1 text-xs text-brand-muted">
                      {steps[index - 1].count > 0
                        ? t("admin.funnelRates", { share: share(step.count, opened), step: share(step.count, steps[index - 1].count) })
                        : t("admin.funnelShare", { share: share(step.count, opened) })}
                    </p>
                  )}
                  {index === weakest && <div className="mt-1.5"><Badge tone="warning">{t("admin.funnelBiggestDrop")}</Badge></div>}
                </li>
              ))}
            </ol>
          );
        }}
      </Report>
    </div>
  );
}
