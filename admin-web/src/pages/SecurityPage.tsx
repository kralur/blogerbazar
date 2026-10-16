import { getCspReports } from "../api/admin";
import { Report } from "../components/Report";
import { Table } from "../components/ui";
import { useI18n } from "../i18n";
import { formatNumber } from "../lib/format";
import { formatTashkentDateTime } from "../lib/time";
import { useLoad } from "../lib/useLoad";

// What the Mini App's page protection (CSP, report-only) would have blocked, as the API collected it since its start.
export function SecurityPage() {
  const { t } = useI18n();
  const { state, reload } = useLoad(getCspReports, "csp");
  return (
    <Report title={t("security.title")} hint={t("security.hint")} loaded={state} onRetry={reload}>
      {(reports) => (
        <Table
          caption={t("security.title")}
          empty={t("security.empty")}
          textColumns={[1, 2]}
          head={[t("security.directive"), t("security.blocked"), t("security.source"), t("security.count"), t("security.last")]}
          rows={reports.map((report) => [
            report.directive,
            <span key="blocked" className="break-all">{report.blocked || "inline"}</span>,
            <span key="source" className="break-all">{report.source || "–"}</span>,
            formatNumber(report.count),
            formatTashkentDateTime(report.lastSeenAtUtc)
          ])}
        />
      )}
    </Report>
  );
}
