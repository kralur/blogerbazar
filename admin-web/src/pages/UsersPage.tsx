import { useEffect, useState } from "react";
import { getUsers, platformRoles, userFilters, type AdminUser, type UserFilter } from "../api/admin";
import { UserLink } from "../components/Link";
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

// People: search by phone, name, @username or Telegram id, with the same filters as the Mini App admin. The server
// returns at most 200, newest first.
export function UsersPage() {
  const { t } = useI18n();
  const [search, setSearch] = useState("");
  const [query, setQuery] = useState("");
  const [filter, setFilter] = useState<UserFilter>("all");
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
      </div>
      <Report title={t("users.title")} hint={t("users.hint")} loaded={state} onRetry={reload}>
        {(users) => (
          <div className="space-y-3">
            <p className="text-sm text-brand-muted">{t("users.count", { count: users.length })}</p>
            <Table
              caption={t("users.title")}
              empty={t("report.empty")}
              textColumns={[2, 3, 6]}
              head={[t("users.name"), t("users.id"), t("users.role"), t("users.profiles"), t("users.registered"), t("users.lastSeen"), t("users.state")]}
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
        )}
      </Report>
    </div>
  );
}
