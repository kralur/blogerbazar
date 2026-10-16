import { useMemo, useState, type ReactNode } from "react";
import { getAuditLog, getUsers, type AdminUser, type AuditEntry } from "../api/admin";
import { Link, UserLink } from "../components/Link";
import { PeriodPicker, presetPeriod, type PeriodChoice } from "../components/PeriodPicker";
import { Report } from "../components/Report";
import { Choice, Table } from "../components/ui";
import { useI18n } from "../i18n";
import { formatTashkentDateTime } from "../lib/time";
import { useLoad } from "../lib/useLoad";
import { complaintPath } from "./ComplaintsPage";

type Group = "all" | "users" | "moderation" | "complaints" | "signIns";
const groups: Group[] = ["all", "users", "moderation", "complaints", "signIns"];
const groupPrefixes: Record<Exclude<Group, "all">, string[]> = {
  users: ["platform-user."],
  moderation: ["blogger-profile.", "business-profile.", "campaign."],
  complaints: ["complaint."],
  signIns: ["admin-web."]
};
const inGroup = (entry: AuditEntry, group: Group) => group === "all" || groupPrefixes[group].some((prefix) => entry.action.startsWith(prefix));

// Enum names the server writes as details ("Admin", "Rejected") shown with the app's words.
const roleNames = ["Member", "Support", "Moderator", "Admin", "Owner"];
const campaignStatusNames = ["Draft", "Published", "Archived", "Rejected"];

// The journal: who did what and to whom, for the chosen Tashkent days (up to 500 entries). It answers "who blocked
// this person", "who closed that complaint", "who signed in to the admin site". Search narrows it by person or action.
export function AuditLogPage() {
  const { t } = useI18n();
  const [period, setPeriod] = useState<PeriodChoice>(() => presetPeriod(30));
  const [group, setGroup] = useState<Group>("all");
  const [search, setSearch] = useState("");
  const { state, reload } = useLoad(async () => {
    const [entries, users] = await Promise.all([getAuditLog(period.from, period.to), getUsers("", "all").catch(() => [] as AdminUser[])]);
    return { entries, users };
  }, `${period.from}|${period.to}`);

  return (
    <div className="space-y-6">
      <PeriodPicker value={period} onChange={setPeriod} />
      <div className="space-y-3">
        <input
          type="search"
          value={search}
          onChange={(event) => setSearch(event.target.value)}
          aria-label={t("journal.search")}
          placeholder={t("journal.search")}
          className="h-11 w-full max-w-xl rounded-[var(--bb-radius-control)] border border-brand-line bg-brand-surface px-4 text-sm text-brand-ink placeholder:text-brand-muted"
        />
        <Choice label={t("journal.filter")} value={group} onChange={setGroup} options={groups.map((value) => ({ value, label: t(`journal.group.${value}`) }))} />
      </div>
      <Report title={t("journal.title")} hint={t("journal.hint")} loaded={state} onRetry={reload}>
        {({ entries, users }) => <Journal entries={entries.filter((entry) => inGroup(entry, group))} users={users} search={search} />}
      </Report>
    </div>
  );
}

type Row = { entry: AuditEntry; who: string; what: string; target: ReactNode; targetText: string; details: string };

function Journal({ entries, users, search }: { entries: AuditEntry[]; users: AdminUser[]; search: string }) {
  const { t } = useI18n();
  const people = useMemo(() => new Map(users.map((user) => [user.telegramUserId, user])), [users]);
  const nameOf = (telegramUserId: number) => people.get(telegramUserId)?.firstName ?? String(telegramUserId);
  const label = (action: string) => {
    const key = `admin.log.${action}`;
    const text = t(key);
    return text === key ? action : text;
  };

  const rows: Row[] = entries.map((entry) => {
    const what = label(entry.action);
    // Deleting an account is written with the account's internal id: the person is the one who did it.
    const personId = entry.action === "platform-user.account-deleted" ? entry.actorTelegramUserId
      : entry.targetType === "PlatformUser" && Number.isSafeInteger(Number(entry.targetId)) ? Number(entry.targetId) : null;
    if (personId !== null) {
      return { entry, who: nameOf(entry.actorTelegramUserId), what, target: <UserLink telegramUserId={personId} name={nameOf(personId)} />, targetText: nameOf(personId), details: details(entry) };
    }
    if (entry.targetType === "Complaint") {
      // Details are "<target type>:<target id>"; the link opens the case.
      const targetType = entry.details?.split(":")[0] ?? "";
      const typeKey = `admin.complaintTarget.${targetType}`;
      const typeLabel = t(typeKey) === typeKey ? "" : t(typeKey);
      const text = typeLabel ? t("journal.complaintAbout", { type: typeLabel }) : t("journal.target.Complaint");
      return { entry, who: nameOf(entry.actorTelegramUserId), what, target: <Link to={complaintPath(entry.targetId)}>{text}</Link>, targetText: text, details: "–" };
    }
    const key = `journal.target.${entry.targetType}`;
    const type = t(key) === key ? entry.targetType : t(key);
    const text = `${type} ${entry.targetId.slice(0, 8)}`;
    return { entry, who: nameOf(entry.actorTelegramUserId), what, target: text, targetText: text, details: details(entry) };
  });

  function details(entry: AuditEntry) {
    if (!entry.details) return "–";
    if (entry.action === "platform-user.role-updated" && roleNames.includes(entry.details)) return t(`admin.role.${entry.details.toLowerCase()}`);
    if (entry.action === "campaign.status-changed" && campaignStatusNames.includes(entry.details)) return t(`admin.campaignStatus.${campaignStatusNames.indexOf(entry.details)}`);
    return entry.details;
  }

  // Search by name, @username, Telegram id or the action's words, on both sides of the entry.
  const needle = search.trim().toLowerCase().replace(/^@/, "");
  const matches = (row: Row) => {
    if (!needle) return true;
    const actor = people.get(row.entry.actorTelegramUserId);
    return [row.who, row.what, row.targetText, row.details, String(row.entry.actorTelegramUserId), row.entry.targetId, actor?.username ?? ""]
      .some((value) => value.toLowerCase().includes(needle));
  };
  const shown = rows.filter(matches);

  return (
    <div className="space-y-3">
      <p className="text-sm text-brand-muted">{t("journal.count", { count: shown.length })}</p>
      <Table
        caption={t("journal.title")}
        empty={t("journal.empty")}
        textColumns={[1, 2, 3, 4]}
        head={[t("journal.when"), t("journal.who"), t("journal.what"), t("journal.target"), t("journal.details")]}
        rows={shown.map((row) => [
          formatTashkentDateTime(row.entry.createdAtUtc),
          <UserLink key="who" telegramUserId={row.entry.actorTelegramUserId} name={row.who} />,
          row.what,
          <span key="target">{row.target}</span>,
          <span key="details" className="break-all">{row.details}</span>
        ])}
      />
    </div>
  );
}
