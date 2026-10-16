import { useMemo, useState } from "react";
import { getAuditLog, getUsers, type AdminUser, type AuditEntry } from "../api/admin";
import { UserLink } from "../components/Link";
import { Report } from "../components/Report";
import { Choice, Table } from "../components/ui";
import { useI18n } from "../i18n";
import { formatTashkentDateTime } from "../lib/time";
import { useLoad } from "../lib/useLoad";

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

// The journal: the latest 200 entries, who did what and to whom. People are named from the user list when known.
export function AuditLogPage() {
  const { t } = useI18n();
  const [group, setGroup] = useState<Group>("all");
  const { state, reload } = useLoad(async () => {
    const [entries, users] = await Promise.all([getAuditLog(), getUsers("", "all").catch(() => [] as AdminUser[])]);
    return { entries, users };
  }, "journal");

  const label = (action: string) => {
    const key = `admin.log.${action}`;
    const text = t(key);
    return text === key ? action : text;
  };
  const details = (entry: AuditEntry) => {
    if (!entry.details) return "–";
    if (entry.action === "platform-user.role-updated" && roleNames.includes(entry.details)) return t(`admin.role.${entry.details.toLowerCase()}`);
    if (entry.action === "campaign.status-changed" && campaignStatusNames.includes(entry.details)) return t(`admin.campaignStatus.${campaignStatusNames.indexOf(entry.details)}`);
    return entry.details;
  };

  return (
    <div className="space-y-6">
      <Choice label={t("journal.filter")} value={group} onChange={setGroup} options={groups.map((value) => ({ value, label: t(`journal.group.${value}`) }))} />
      <Report title={t("journal.title")} hint={t("journal.hint")} loaded={state} onRetry={reload}>
        {({ entries, users }) => <JournalTable entries={entries.filter((entry) => inGroup(entry, group))} users={users} label={label} details={details} />}
      </Report>
    </div>
  );
}

function JournalTable({ entries, users, label, details }: { entries: AuditEntry[]; users: AdminUser[]; label: (action: string) => string; details: (entry: AuditEntry) => string }) {
  const { t } = useI18n();
  const names = useMemo(() => new Map(users.map((user) => [user.telegramUserId, user.firstName])), [users]);
  const person = (telegramUserId: number) => <UserLink telegramUserId={telegramUserId} name={names.get(telegramUserId) ?? String(telegramUserId)} />;
  const target = (entry: AuditEntry) => {
    const id = Number(entry.targetId);
    if (entry.targetType === "PlatformUser" && Number.isSafeInteger(id)) return person(id);
    const key = `journal.target.${entry.targetType}`;
    const type = t(key);
    return `${type === key ? entry.targetType : type} ${entry.targetId.slice(0, 8)}`;
  };
  return (
    <Table
      caption={t("journal.title")}
      empty={t("report.empty")}
      textColumns={[1, 2, 3, 4]}
      head={[t("journal.when"), t("journal.who"), t("journal.what"), t("journal.target"), t("journal.details")]}
      rows={entries.map((entry) => [
        formatTashkentDateTime(entry.createdAtUtc),
        <span key="who">{person(entry.actorTelegramUserId)}</span>,
        label(entry.action),
        <span key="target">{target(entry)}</span>,
        details(entry)
      ])}
    />
  );
}
