import { useState } from "react";
import { dealFilters, getDeals, type DealFilter } from "../api/admin";
import { UserLink } from "../components/Link";
import { Report } from "../components/Report";
import { Choice, Table } from "../components/ui";
import { useI18n } from "../i18n";
import { formatMoney } from "../lib/format";
import { formatTashkentDate } from "../lib/time";
import { useLoad } from "../lib/useLoad";

const dealStatuses = ["active", "completed", "cancelled"];
export const dealStatusLabel = (status: number, t: (key: string) => string) => t(`deals.state.${dealStatuses[status] ?? "active"}`);

// Every deal, newest first (at most 200): both sides open their admin cards. Read only; deals change in the app.
export function DealsPage() {
  const { t } = useI18n();
  const [filter, setFilter] = useState<DealFilter>("all");
  const { state, reload } = useLoad(() => getDeals(filter), filter);
  return (
    <div className="space-y-6">
      <Choice label={t("deals.filter")} value={filter} onChange={setFilter} options={dealFilters.map((value) => ({ value, label: t(`deals.filter.${value}`) }))} />
      <Report title={t("deals.title")} hint={t("deals.hint")} loaded={state} onRetry={reload}>
        {(deals) => (
          <Table
            caption={t("deals.title")}
            empty={t("report.empty")}
            textColumns={[1, 2, 3, 4]}
            head={[t("deals.date"), t("deals.what"), t("deals.business"), t("deals.creator"), t("deals.status"), t("deals.price"), t("deals.completedAt")]}
            rows={deals.map((deal) => [
              formatTashkentDate(deal.createdAtUtc),
              deal.title ?? t("deals.offer"),
              <UserLink key="business" telegramUserId={deal.businessTelegramUserId} name={deal.businessName} />,
              <span key="creator"><UserLink telegramUserId={deal.creatorTelegramUserId} name={deal.creatorName} /> <span className="text-brand-muted">· {t(`admin.userProfileRole.${deal.creatorRole}`)}</span></span>,
              dealStatusLabel(deal.status, t),
              deal.agreedPrice === null ? "–" : formatMoney(deal.agreedPrice, t),
              formatTashkentDate(deal.completedAtUtc) || "–"
            ])}
          />
        )}
      </Report>
    </div>
  );
}
