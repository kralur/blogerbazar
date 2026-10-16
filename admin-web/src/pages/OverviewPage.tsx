import type { AdminWebSession } from "../api/session";
import { getDaily, getDashboard } from "../api/analytics";
import { Report } from "../components/Report";
import { Card, Tile } from "../components/ui";
import { useI18n } from "../i18n";
import { formatNumber, tashkentDate } from "../lib/format";
import { useLoad } from "../lib/useLoad";
import { DailyCharts } from "./AnalyticsPage";

// The main numbers at a glance: totals, the last 7 days, today (a Tashkent day, from the daily report) and two
// 30-day charts. Everything else is on the analytics page.
export function OverviewPage({ session, onNavigate }: { session: AdminWebSession; onNavigate: (path: string) => void }) {
  const { t } = useI18n();
  const dashboard = useLoad(getDashboard, "dashboard");
  const from = tashkentDate(-29);
  const to = tashkentDate();
  const daily = useLoad(() => getDaily({ from, to }), `${from}|${to}`);

  return (
    <div className="space-y-6">
      <p className="text-base text-brand-muted">{t("overview.welcome", { name: session.firstName })}</p>
      <Report title={t("overview.totals")} loaded={dashboard.state} onRetry={dashboard.reload}>
        {(numbers) => (
          <div className="space-y-4">
            <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
              <Tile label={t("overview.users")} value={formatNumber(numbers.users)} />
              <Tile label={t("overview.bloggers")} value={formatNumber(numbers.bloggers)} />
              <Tile label={t("overview.businesses")} value={formatNumber(numbers.businesses)} />
              <Tile label={t("overview.publishedCampaigns")} value={formatNumber(numbers.publishedCampaigns)} />
              <Tile label={t("overview.completedDeals")} value={formatNumber(numbers.completedDeals)} />
              <Tile label={t("overview.openComplaints")} value={formatNumber(numbers.openComplaints)} />
              <Tile label={t("overview.promotedBloggers")} value={formatNumber(numbers.promotedBloggers)} />
              <Tile label={t("overview.promotedCampaigns")} value={formatNumber(numbers.promotedCampaigns)} />
            </div>
            <h3 className="text-sm font-bold">{t("overview.week")}</h3>
            <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
              <Tile label={t("overview.newUsers")} value={formatNumber(numbers.newUsersWeek)} />
              <Tile label={t("overview.active")} value={formatNumber(numbers.activeWeek)} />
              <Tile label={t("overview.newDeals")} value={formatNumber(numbers.newDealsWeek)} />
              <Tile label={t("overview.completedDealsWeek")} value={formatNumber(numbers.completedDealsWeek)} />
            </div>
          </div>
        )}
      </Report>
      <Report title={t("overview.today")} hint={t("overview.todayHint")} loaded={daily.state} onRetry={daily.reload}>
        {(report) => {
          const today = report.days[report.days.length - 1];
          return (
            <div className="space-y-4">
              <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
                <Tile label={t("overview.newUsers")} value={formatNumber(today?.newUsers ?? 0)} />
                <Tile label={t("overview.active")} value={formatNumber(today?.lastSeen ?? 0)} />
                <Tile label={t("overview.newDeals")} value={formatNumber(today?.deals ?? 0)} />
                <Tile label={t("overview.completedDealsWeek")} value={formatNumber(today?.completedDeals ?? 0)} />
              </div>
              <DailyCharts daily={report} metrics={["newUsers", "deals"]} />
            </div>
          );
        }}
      </Report>
      <Card>
        <a
          href="/analytics"
          className="text-sm font-semibold text-brand-blue underline-offset-4 hover:underline"
          onClick={(event) => {
            event.preventDefault();
            onNavigate("/analytics");
          }}
        >
          {t("overview.allAnalytics")}
        </a>
      </Card>
    </div>
  );
}

export function NotFoundPage({ onBack }: { onBack: () => void }) {
  const { t } = useI18n();
  return (
    <Card>
      <h2 className="text-lg font-extrabold">{t("notFound.title")}</h2>
      <button type="button" className="mt-3 text-sm font-semibold text-brand-blue underline-offset-4 hover:underline" onClick={onBack}>{t("notFound.back")}</button>
    </Card>
  );
}
