import { useEffect, useState, type ReactNode } from "react";
import { getUsers, platformRoles, userFilters, type AdminUser, type UserFilter } from "../api/admin";
import { UserLink } from "../components/Link";
import { ExportButton } from "../components/ExportButton";
import { Report } from "../components/Report";
import { Badge, Choice, Table } from "../components/ui";
import { useI18n } from "../i18n";
import { formatTashkentDate, formatTashkentDateTime } from "../lib/time";
import { useLoad } from "../lib/useLoad";

export const roleLabel = (role: number, t: (key: string) => string) => t(`admin.role.${platformRoles[role] ?? "member"}`);

export function profileLabels(user: Pick<AdminUser, "hasBloggerProfile" | "hasBusinessProfile" | "hasBrandFaceProfile">, t: (key: string) => string) {
  return [
    user.hasBloggerProfile && t("admin.userProfileRole.blogger"),
    user.hasBrandFaceProfile && t("admin.userProfileRole.brandFace"),
    user.hasBusinessProfile && t("admin.userProfileRole.business")
  ].filter(Boolean).join(", ");
}

export function UserState({ user }: { user: Pick<AdminUser, "isBlocked" | "isDeleted"> }) {
  const { t } = useI18n();
  if (user.isDeleted) return <Badge>{t("users.deleted")}</Badge>;
  if (user.isBlocked) return <Badge tone="danger">{t("users.blocked")}</Badge>;
  return <Badge tone="success">{t("users.active")}</Badge>;
}

type SortKey = "registered" | "lastSeen" | "name";
type Activity = "all" | "active7" | "inactive30" | "never";
const activities: Activity[] = ["all", "active7", "inactive30", "never"];
const dayMs = 86_400_000;

// Last app open against "now": within 7 days, more than 30 days ago, or never recorded (before D56 or never opened).
export function matchesActivity(user: Pick<AdminUser, "lastSeenAtUtc">, activity: Activity, now = Date.now()) {
  const seen = user.lastSeenAtUtc ? Date.parse(user.lastSeenAtUtc) : null;
  if (activity === "active7") return seen !== null && now - seen <= 7 * dayMs;
  if (activity === "inactive30") return seen !== null && now - seen > 30 * dayMs;
  if (activity === "never") return seen === null;
  return true;
}

function sortUsers(users: AdminUser[], key: SortKey, descending: boolean) {
  const value = (user: AdminUser) => key === "name" ? user.firstName.toLocaleLowerCase()
    : key === "registered" ? user.createdAtUtc : user.lastSeenAtUtc ?? "";
  return [...users].sort((a, b) => {
    const order = value(a) < value(b) ? -1 : value(a) > value(b) ? 1 : 0;
    return descending ? -order : order;
  });
}

// People: search by phone, name, @username or Telegram id, with the same filters as the Mini App admin. The server
// returns at most 200, newest first.
export function UsersPage() {
  const { t } = useI18n();
  const [search, setSearch] = useState("");
  const [query, setQuery] = useState("");
  const [filter, setFilter] = useState<UserFilter>("all");
  const [activity, setActivity] = useState<Activity>("all");
  const [sort, setSort] = useState<{ key: SortKey; descending: boolean }>({ key: "registered", descending: true });
  const sortBy = (key: SortKey) => setSort((current) => ({ key, descending: current.key === key ? !current.descending : key !== "name" }));
  const header = (key: SortKey, text: string): ReactNode => (
    <button type="button" className="inline-flex items-center gap-1 font-semibold hover:text-brand-ink" onClick={() => sortBy(key)}>
      {text}<span aria-hidden="true">{sort.key === key ? (sort.descending ? "↓" : "↑") : "↕"}</span>
    </button>
  );
  // Typing waits 300 ms before asking the server.
  useEffect(() => {
    const timer = window.setTimeout(() => setQuery(search), 300);
    return () => window.clearTimeout(timer);
  }, [search]);
  const { state, reload } = useLoad(() => getUsers(query, filter), `${query}|${filter}`);

  return (
    <div className="space-y-6">
      <div className="space-y-3">
        <input
          type="search"
          value={search}
          onChange={(event) => setSearch(event.target.value)}
          aria-label={t("users.search")}
          placeholder={t("users.search")}
          className="h-11 w-full max-w-xl rounded-[var(--bb-radius-control)] border border-brand-line bg-brand-surface px-4 text-sm text-brand-ink placeholder:text-brand-muted"
        />
        <Choice label={t("users.filter")} value={filter} onChange={setFilter} options={userFilters.map((value) => ({ value, label: t(`admin.userFilter.${value}`) }))} />
        <Choice label={t("users.activity")} value={activity} onChange={setActivity} options={activities.map((value) => ({ value, label: t(`users.activity.${value}`) }))} />
      </div>
      <Report
        title={t("users.title")}
        hint={t("users.hint")}
        loaded={state}
        onRetry={reload}
        actions={state.status === "ready" ? <ExportButton kind="users" table={() => ({
          head: [t("users.name"), t("users.username"), t("users.id"), t("user.phone"), t("users.role"), t("users.profiles"), t("users.registered"), t("users.lastSeen"), t("user.source"), t("users.state")],
          personalColumns: [1, 2, 3],
          rows: sortUsers(state.data.filter((user) => matchesActivity(user, activity)), sort.key, sort.descending).map((user) => [
            user.firstName, user.username ? `@${user.username}` : "", String(user.telegramUserId), user.verifiedPhone, roleLabel(user.role, t), profileLabels(user, t),
            formatTashkentDateTime(user.createdAtUtc), formatTashkentDateTime(user.lastSeenAtUtc), user.source,
            user.isDeleted ? t("users.deleted") : user.isBlocked ? t("users.blocked") : t("users.active")
          ])
        })} /> : undefined}
      >
        {(loaded) => {
          const users = sortUsers(loaded.filter((user) => matchesActivity(user, activity)), sort.key, sort.descending);
          const sortIndex = { name: 0, registered: 4, lastSeen: 5 }[sort.key];
          return (
          <div className="space-y-3">
            <p className="text-sm text-brand-muted">{t("users.count", { count: users.length })}</p>
            <Table
              sortedBy={{ index: sortIndex, direction: sort.descending ? "descending" : "ascending" }}
              caption={t("users.title")}
              empty={t("report.empty")}
              textColumns={[2, 3, 6]}
              head={[header("name", t("users.name")), t("users.id"), t("users.role"), t("users.profiles"), header("registered", t("users.registered")), header("lastSeen", t("users.lastSeen")), t("users.state")]}
              rows={users.map((user) => [
                <span key="name" className="block min-w-[9rem]">
                  <UserLink telegramUserId={user.telegramUserId} name={user.firstName} />
                  {user.username && <span className="block break-all text-xs font-normal text-brand-muted">@{user.username}</span>}
                </span>,
                String(user.telegramUserId),
                roleLabel(user.role, t),
                profileLabels(user, t) || "–",
                formatTashkentDate(user.createdAtUtc),
                formatTashkentDateTime(user.lastSeenAtUtc) || "–",
                <UserState key="state" user={user} />
              ])}
            />
          </div>
          );
        }}
      </Report>
    </div>
  );
}
