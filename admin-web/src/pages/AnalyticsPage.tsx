import { useState } from "react";
import { getDaily, getGeography, getMoney, getRetention, getSources, type AdminDaily, type DailyMetric, type GeographyRow } from "../api/analytics";
import { Chart } from "../components/Chart";
import { asPeriod, PeriodPicker, presetPeriod, type PeriodChoice } from "../components/PeriodPicker";
import { Report } from "../components/Report";
import { Choice, Table, Tile } from "../components/ui";
import { useI18n } from "../i18n";
import { formatCompact, formatDate, formatDay, formatMoney, formatMonth, formatNumber, percent, taxonomyLabel } from "../lib/format";
import { useLoad } from "../lib/useLoad";

export const dailyMetrics: DailyMetric[] = ["newUsers", "lastSeen", "campaigns", "applications", "offers", "deals", "completedDeals"];
type View = "chart" | "table";
const dash = "–";

// Analytics: everything by Tashkent day for the chosen period, then money by month, cities, categories and
// ad labels. Every chart has the same numbers as a table.
export function AnalyticsPage() {
  const [period, setPeriod] = useState<PeriodChoice>(() => presetPeriod(30));
  return (
    <div className="space-y-6">
      <PeriodPicker value={period} onChange={setPeriod} />
      <DailyReport period={period} />
      <RetentionReport period={period} />
      <MoneyReport />
      <GeographyReport />
      <SourcesReport />
    </div>
  );
}

function ViewChoice({ value, onChange }: { value: View; onChange: (value: View) => void }) {
  const { t } = useI18n();
  return <Choice label={t("report.view")} value={value} onChange={onChange} options={[{ value: "chart", label: t("report.viewChart") }, { value: "table", label: t("report.viewTable") }]} />;
}

export function DailyCharts({ daily, metrics }: { daily: AdminDaily; metrics: DailyMetric[] }) {
  const { t } = useI18n();
  return (
    <div className="grid gap-4 md:grid-cols-2">
      {metrics.map((metric) => {
        const total = daily.days.reduce((sum, day) => sum + day[metric], 0);
        const name = t(`analytics.metric.${metric}`);
        return (
          <div key={metric} className="min-w-0 rounded-[var(--bb-radius-control)] border border-brand-line p-4">
            <div className="flex items-baseline justify-between gap-3">
              <h3 className="min-w-0 text-sm font-bold">{name}</h3>
              <p className="shrink-0 text-lg font-extrabold tabular-nums">{formatNumber(total)}</p>
            </div>
            <p className="mb-2 text-xs text-brand-muted">{t(`analytics.metricHint.${metric}`)}</p>
            <Chart
              kind="line"
              height={160}
              points={daily.days.map((day) => ({ label: formatDay(day.date), value: day[metric] }))}
              summary={t("report.chartSummary", { name, total: formatNumber(total), from: formatDate(daily.from), to: formatDate(daily.to) })}
              formatValue={formatNumber}
            />
          </div>
        );
      })}
    </div>
  );
}

function DailyReport({ period }: { period: PeriodChoice }) {
  const { t } = useI18n();
  const [view, setView] = useState<View>("chart");
  const { state, reload } = useLoad(() => getDaily(asPeriod(period)), `${period.from}|${period.to}`);
  return (
    <Report title={t("analytics.dailyTitle")} hint={t("analytics.dailyHint")} loaded={state} onRetry={reload} actions={<ViewChoice value={view} onChange={setView} />}>
      {(daily) => view === "chart"
        ? <DailyCharts daily={daily} metrics={dailyMetrics} />
        : <Table
            caption={t("analytics.dailyTitle")}
            head={[t("report.date"), ...dailyMetrics.map((metric) => t(`analytics.metric.${metric}`))]}
            rows={[...daily.days].reverse().map((day) => [formatDate(day.date), ...dailyMetrics.map((metric) => formatNumber(day[metric]))])}
          />}
    </Report>
  );
}

function RetentionReport({ period }: { period: PeriodChoice }) {
  const { t } = useI18n();
  const { state, reload } = useLoad(() => getRetention(asPeriod(period)), `${period.from}|${period.to}`);
  return (
    <Report title={t("retention.title")} hint={t("retention.hint")} loaded={state} onRetry={reload}>
      {(retention) => (
        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
          <Tile label={t("retention.cohort")} value={formatNumber(retention.cohort)} />
          {retention.steps.map((step) => (
            <Tile
              key={step.days}
              label={t("retention.after", { count: step.days })}
              value={percent(step.returned, step.eligible) ?? dash}
              note={step.eligible > 0 ? t("retention.of", { returned: formatNumber(step.returned), eligible: formatNumber(step.eligible) }) : t("retention.tooEarly")}
            />
          ))}
        </div>
      )}
    </Report>
  );
}

function MoneyReport() {
  const { t, language } = useI18n();
  const [view, setView] = useState<View>("chart");
  const { state, reload } = useLoad(() => getMoney(12), "12");
  return (
    <Report title={t("money.title")} hint={t("money.hint")} loaded={state} onRetry={reload} actions={<ViewChoice value={view} onChange={setView} />}>
      {({ months }) => {
        const total = months.reduce((sum, month) => sum + month.total, 0);
        const priced = months.reduce((sum, month) => sum + month.pricedDeals, 0);
        const completed = months.reduce((sum, month) => sum + month.completedDeals, 0);
        return (
          <div className="space-y-4">
            <div className="grid gap-3 sm:grid-cols-3">
              <Tile label={t("money.total")} value={formatMoney(total, t)} />
              <Tile label={t("money.average")} value={priced > 0 ? formatMoney(Math.round(total / priced), t) : dash} />
              <Tile label={t("money.completed")} value={formatNumber(completed)} note={t("money.priced", { count: formatNumber(priced) })} />
            </div>
            {view === "chart"
              ? <Chart
                  kind="bar"
                  points={months.map((month) => ({ label: t(`month.short.${Number(month.month.slice(5, 7))}`), detail: formatMonth(month.month, t), value: month.total }))}
                  summary={t("money.summary", { total: formatMoney(total, t) })}
                  formatValue={(value) => formatMoney(value, t)}
                  formatAxis={(value) => formatCompact(value, language)}
                  axisWidth={72}
                />
              : <Table
                  caption={t("money.title")}
                  head={[t("money.month"), t("money.completed"), t("money.pricedColumn"), t("money.total"), t("money.average")]}
                  rows={[...months].reverse().map((month) => [
                    formatMonth(month.month, t),
                    formatNumber(month.completedDeals),
                    formatNumber(month.pricedDeals),
                    formatMoney(month.total, t),
                    month.average === null ? dash : formatMoney(month.average, t)
                  ])}
                />}
          </div>
        );
      }}
    </Report>
  );
}

function GeographyTable({ kind, rows }: { kind: "city" | "category"; rows: GeographyRow[] }) {
  const { t } = useI18n();
  const title = kind === "city" ? t("geography.cities") : t("geography.categories");
  return (
    <div className="min-w-0">
      <h3 className="mb-2 text-sm font-bold">{title}</h3>
      <Table
        caption={title}
        empty={t("report.empty")}
        head={[kind === "city" ? t("geography.city") : t("geography.category"), t("geography.bloggers"), t("geography.brandFaces"), t("geography.campaigns"), t("geography.deals")]}
        rows={rows.map((row) => [taxonomyLabel(kind, row.key, t), formatNumber(row.bloggers), formatNumber(row.brandFaces), formatNumber(row.campaigns), formatNumber(row.deals)])}
      />
    </div>
  );
}

function GeographyReport() {
  const { t } = useI18n();
  const { state, reload } = useLoad(getGeography, "geography");
  return (
    <Report title={t("geography.title")} hint={t("geography.hint")} loaded={state} onRetry={reload}>
      {(geography) => (
        <div className="grid gap-6 lg:grid-cols-2">
          <GeographyTable kind="city" rows={geography.cities} />
          <GeographyTable kind="category" rows={geography.categories} />
        </div>
      )}
    </Report>
  );
}

function SourcesReport() {
  const { t } = useI18n();
  const { state, reload } = useLoad(getSources, "sources");
  return (
    <Report title={t("sources.title")} hint={t("sources.hint")} loaded={state} onRetry={reload}>
      {({ sources }) => (
        <Table
          caption={t("sources.title")}
          empty={t("report.empty")}
          head={[t("sources.label"), t("sources.users"), t("sources.withProfile"), t("sources.withDeal"), t("sources.conversion")]}
          rows={sources.map((row) => [
            row.source ?? t("sources.none"),
            formatNumber(row.users),
            formatNumber(row.withProfile),
            formatNumber(row.withDeal),
            percent(row.withDeal, row.users) ?? dash
          ])}
        />
      )}
    </Report>
  );
}
