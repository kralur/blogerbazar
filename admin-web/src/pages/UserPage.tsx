import { useEffect, useState } from "react";
import { getUser, ownerRole, platformRoles, setUserBlocked, setUserRole, type AdminUserDetails } from "../api/admin";
import type { AdminWebSession } from "../api/session";
import { useActions } from "../components/Actions";
import { Link } from "../components/Link";
import { Report } from "../components/Report";
import { Badge, Button, Card, Table } from "../components/ui";
import { useI18n } from "../i18n";
import { formatMoney, formatNumber } from "../lib/format";
import { formatTashkentDate, formatTashkentDateTime } from "../lib/time";
import { useLoad } from "../lib/useLoad";
import { dealStatusLabel } from "./DealsPage";
import { roleLabel, UserState } from "./UsersPage";

// Roles the owner can give here; the owner role itself comes only from the deployment settings.
const assignableRoles = [0, 1, 2, 3];

// One person: account, profiles, deals and complaints, and, for the owner only, the role and blocking.
export function UserPage({ telegramUserId, session }: { telegramUserId: number; session: AdminWebSession }) {
  const { t } = useI18n();
  const { state, reload, refresh } = useLoad(() => getUser(telegramUserId), String(telegramUserId));
  const actions = useActions(refresh);

  return (
    <div className="space-y-6">
      <Link to="/users">{t("user.back")}</Link>
      {actions.status}
      <Report title={t("user.title")} loaded={state} onRetry={reload}>
        {(details) => <UserDetails details={details} />}
      </Report>
      {state.status === "ready" && <UserActions details={state.data} session={session} ask={actions.ask} />}
      {actions.dialog}
    </div>
  );
}

function Fact({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="min-w-0">
      <dt className="text-xs text-brand-muted">{label}</dt>
      <dd className="mt-0.5 break-words text-sm font-semibold">{children}</dd>
    </div>
  );
}

function UserDetails({ details }: { details: AdminUserDetails }) {
  const { t } = useI18n();
  const { user } = details;
  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-center gap-3">
        <h3 className="min-w-0 break-words text-xl font-extrabold">{user.firstName}</h3>
        <UserState user={user} />
      </div>
      <dl className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        <Fact label={t("users.id")}>{user.telegramUserId}</Fact>
        <Fact label={t("users.username")}>
          {user.username ? <a className="text-brand-blue underline-offset-4 hover:underline" href={`https://t.me/${encodeURIComponent(user.username)}`} target="_blank" rel="noopener noreferrer">@{user.username}</a> : "–"}
        </Fact>
        <Fact label={t("user.phone")}>{user.verifiedPhone ?? "–"}</Fact>
        <Fact label={t("users.role")}>{roleLabel(user.role, t)}</Fact>
        <Fact label={t("users.registered")}>{formatTashkentDateTime(user.createdAtUtc)}</Fact>
        <Fact label={t("users.lastSeen")}>{formatTashkentDateTime(user.lastSeenAtUtc) || "–"}</Fact>
        <Fact label={t("user.source")}>{user.source ?? t("sources.none")}</Fact>
        {user.isDeleted && <Fact label={t("users.deleted")}>{formatTashkentDate(user.deletedAtUtc)}</Fact>}
      </dl>

      <section className="space-y-2">
        <h3 className="text-sm font-bold">{t("user.profiles")}</h3>
        {details.profiles.length === 0
          ? <p className="text-sm text-brand-muted">{t("user.noProfiles")}</p>
          : <ul className="flex flex-wrap gap-2">
              {details.profiles.map((profile) => (
                <li key={profile.id} className="rounded-[var(--bb-radius-control)] border border-brand-line px-3 py-2 text-sm">
                  <span className="font-semibold">{profile.name}</span>
                  <span className="text-brand-muted"> · {t(`admin.userProfileRole.${profile.role}`)}</span>
                  {profile.isHidden && <> <Badge>{t("user.hidden")}</Badge></>}
                  {profile.isDeleted && <> <Badge>{t("users.deleted")}</Badge></>}
                </li>
              ))}
            </ul>}
      </section>

      <section className="space-y-2">
        <h3 className="text-sm font-bold">{t("user.deals")}</h3>
        <p className="text-sm text-brand-muted">{t("user.dealCounts", { active: details.activeDeals, completed: details.completedDeals, cancelled: details.cancelledDeals })}</p>
        <Table
          caption={t("user.deals")}
          empty={t("user.noDeals")}
          textColumns={[1, 2, 3]}
          head={[t("deals.date"), t("deals.what"), t("user.partner"), t("deals.status"), t("deals.price")]}
          rows={details.recentDeals.map((deal) => [
            formatTashkentDate(deal.createdAtUtc),
            deal.title ?? t("deals.offer"),
            deal.partnerName ?? t("user.deletedAccount"),
            dealStatusLabel(deal.status, t),
            deal.agreedPrice === null ? "–" : formatMoney(deal.agreedPrice, t)
          ])}
        />
      </section>

      <section className="space-y-1">
        <h3 className="text-sm font-bold">{t("user.complaints")}</h3>
        <p className="text-sm text-brand-muted">{t("user.complaintCounts", { against: formatNumber(details.complaintsAgainst), open: formatNumber(details.openComplaintsAgainst), filed: formatNumber(details.complaintsFiled) })}</p>
      </section>
    </div>
  );
}

function UserActions({ details, session, ask }: { details: AdminUserDetails; session: AdminWebSession; ask: ReturnType<typeof useActions>["ask"] }) {
  const { t } = useI18n();
  const { user } = details;
  const [role, setRole] = useState(user.role);
  useEffect(() => setRole(user.role), [user.role]);

  let note: string | null = null;
  if (user.role === ownerRole) note = t("user.ownerProtected");
  else if (session.role !== "owner") note = t("user.ownerOnly");
  else if (user.isDeleted) note = t("user.deletedNoActions");

  return (
    <Card className="space-y-4">
      <h2 className="text-lg font-extrabold">{t("user.actions")}</h2>
      {note ? <p className="text-sm text-brand-muted">{note}</p> : (
        <div className="space-y-5">
          <div className="flex flex-wrap items-end gap-3">
            <label className="grid gap-1 text-xs text-brand-muted">
              {t("users.role")}
              <select
                className="h-11 min-w-[12rem] rounded-[var(--bb-radius-control)] border border-brand-line bg-brand-surface px-3 text-sm text-brand-ink"
                value={role}
                onChange={(event) => setRole(Number(event.target.value))}
              >
                {assignableRoles.map((value) => <option key={value} value={value}>{t(`admin.role.${platformRoles[value]}`)}</option>)}
              </select>
            </label>
            <Button
              disabled={role === user.role}
              onClick={() => ask({
                title: t("user.roleTitle"),
                text: t("user.roleText", { name: user.firstName, from: roleLabel(user.role, t), to: roleLabel(role, t) }),
                confirmLabel: t("user.roleConfirm"),
                danger: role >= 3 || user.role >= 3,
                run: () => setUserRole(user.telegramUserId, role),
                done: t("user.roleDone")
              })}
            >
              {t("user.roleChange")}
            </Button>
          </div>
          <div className="flex flex-wrap items-center gap-3">
            {user.isBlocked
              ? <Button onClick={() => ask({
                  title: t("user.unblockTitle"),
                  text: t("user.unblockText", { name: user.firstName }),
                  confirmLabel: t("user.unblock"),
                  run: () => setUserBlocked(user.telegramUserId, false),
                  done: t("user.unblockDone")
                })}>{t("user.unblock")}</Button>
              : <Button variant="danger" onClick={() => ask({
                  title: t("user.blockTitle"),
                  text: t("user.blockText", { name: user.firstName }),
                  confirmLabel: t("user.block"),
                  danger: true,
                  run: () => setUserBlocked(user.telegramUserId, true),
                  done: t("user.blockDone")
                })}>{t("user.block")}</Button>}
          </div>
        </div>
      )}
    </Card>
  );
}
