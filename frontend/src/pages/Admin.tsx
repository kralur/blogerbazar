import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { ApiError } from "../api/client";
import { closeAdminComplaint, getAdminAuditLogs, getAdminBloggers, getAdminBusinesses, getAdminCampaigns, getAdminComplaints, getAdminDashboard, getAdminUsers, setAdminUserBlocked, updateAdminUserRole, type AdminAuditLog, type AdminBloggerProfile, type AdminBusiness, type AdminCampaign, type AdminComplaint, type AdminDashboard, type AdminPlatformUser, type AdminUserFilter } from "../api/marketplace";
import { Avatar, Badge, BottomNav, Button, Card, ErrorState, Icon, LoadingState, Toast } from "../components/ui";
import { useI18n } from "../i18n";
import { formatDate } from "../lib/currency";
import { useProfileDataRefresh } from "../hooks/useProfileDataRefresh";
import { PageHeader } from "../components/PageHeader";
import { useTelegram } from "../telegram/TelegramProvider";

const roleKeys = ["admin.role.member", "admin.role.support", "admin.role.moderator", "admin.role.admin", "admin.role.owner"];
const sections = ["users", "bloggers", "businesses", "campaigns", "complaints", "log"] as const;
type Section = typeof sections[number];
const filterFromHash = (): AdminUserFilter => {
  const requested = new URLSearchParams(window.location.hash.split("?")[1] ?? "").get("filter");
  return userFilters.includes(requested as AdminUserFilter) ? requested as AdminUserFilter : "all";
};
const isSection = (value?: string): value is Section => Boolean(value && (sections as readonly string[]).includes(value));

type Lists = { users: AdminPlatformUser[]; bloggers: AdminBloggerProfile[]; businesses: AdminBusiness[]; campaigns: AdminCampaign[]; complaints: AdminComplaint[]; log: AdminAuditLog[] };
const emptyLists: Lists = { users: [], bloggers: [], businesses: [], campaigns: [], complaints: [], log: [] };
const userFilters: AdminUserFilter[] = ["all", "new", "bloggers", "businesses", "brandFaces", "admins", "blocked", "deleted"];

// The admin home is a grid of tiles; each tile opens its own list at #/admin/<section>, so nothing needs long scrolling.
export function Admin({ section: rawSection }: { section?: string }) {
  const { t } = useI18n();
  const section = isSection(rawSection) ? rawSection : null;
  const [dashboard, setDashboard] = useState<AdminDashboard | null>(null);
  const [lists, setLists] = useState<Lists>(emptyLists);
  const [listLoading, setListLoading] = useState(false);
  const [closing, setClosing] = useState<string | null>(null);
  const [failed, setFailed] = useState(false);
  // Only a refused dashboard means "no access"; any other failure is a load error.
  const [denied, setDenied] = useState(false);
  const [toast, setToast] = useState("");
  const [toastTone, setToastTone] = useState<"success" | "error">("success");
  const roleLabels = useMemo(() => roleKeys.map((key) => t(key)), [t]);
  const { openLink } = useTelegram();
  // User search (phone, name, @username, Telegram id) and filter; typing waits 300 ms before asking the server.
  const [userSearch, setUserSearch] = useState("");
  // "#/admin/users?filter=new" (the link from "Activity") opens the list already filtered.
  const [userFilter, setUserFilter] = useState<AdminUserFilter>(() => filterFromHash());
  // The admin page stays mounted between sections, so the link's filter is read again on each section change.
  useEffect(() => { if (section === "users") setUserFilter(filterFromHash()); }, [section]);
  const userQuery = useRef({ search: "", filter: "all" as AdminUserFilter });
  userQuery.current = { search: userSearch, filter: userFilter };

  const loadSection = useCallback(async (target: Section | null) => {
    if (!target) return;
    setListLoading(true);
    try {
      if (target === "users") { const users = await getAdminUsers(userQuery.current.search, userQuery.current.filter); setLists((current) => ({ ...current, users })); }
      if (target === "bloggers") { const bloggers = await getAdminBloggers(); setLists((current) => ({ ...current, bloggers })); }
      if (target === "businesses") { const businesses = await getAdminBusinesses(); setLists((current) => ({ ...current, businesses })); }
      if (target === "campaigns") { const campaigns = await getAdminCampaigns(); setLists((current) => ({ ...current, campaigns })); }
      if (target === "complaints") { const complaints = await getAdminComplaints(); setLists((current) => ({ ...current, complaints })); }
      // The journal names people, so it needs the user list too.
      if (target === "log") { const [log, users] = await Promise.all([getAdminAuditLogs(), getAdminUsers().catch(() => [] as AdminPlatformUser[])]); setLists((current) => ({ ...current, log, users })); }
    } catch {
      setToastTone("error");
      setToast(t("common.openFailed"));
    } finally {
      setListLoading(false);
    }
  }, [t]);

  const load = useCallback(() => {
    setFailed(false);
    setDenied(false);
    getAdminDashboard()
      .then(setDashboard)
      .catch((error) => { if (error instanceof ApiError && (error.status === 401 || error.status === 403)) setDenied(true); else setFailed(true); });
    void loadSection(section);
  }, [loadSection, section]);
  useEffect(load, [load]);
  useProfileDataRefresh(load);
  useEffect(() => {
    if (section !== "users") return;
    const timer = window.setTimeout(() => void loadSection("users"), 300);
    return () => window.clearTimeout(timer);
  }, [loadSection, section, userFilter, userSearch]);

  const updateRole = async (telegramUserId: number, role: number) => {
    try {
      const user = await updateAdminUserRole(telegramUserId, role);
      setLists((current) => ({ ...current, users: current.users.map((item) => item.telegramUserId === user.telegramUserId ? user : item) }));
      setToastTone("success");
      setToast(t("admin.roleUpdated"));
    } catch { setToastTone("error"); setToast(t("admin.roleUpdateDenied")); }
  };
  const toggleBlock = async (user: AdminPlatformUser) => {
    try {
      const updated = await setAdminUserBlocked(user.telegramUserId, !user.isBlocked);
      setLists((current) => ({ ...current, users: current.users.map((item) => item.telegramUserId === updated.telegramUserId ? updated : item) }));
      setToastTone("success");
      setToast(updated.isBlocked ? t("admin.userBlocked") : t("admin.userUnblocked"));
    } catch { setToastTone("error"); setToast(t("admin.userUpdateDenied")); }
  };
  const closeComplaint = async (complaint: AdminComplaint, status: "resolved" | "dismissed") => {
    if (closing) return;
    setClosing(complaint.id);
    try {
      await closeAdminComplaint(complaint.id, status);
      setLists((current) => ({ ...current, complaints: current.complaints.filter((item) => item.id !== complaint.id) }));
      setDashboard((current) => current && current.openComplaints ? { ...current, openComplaints: current.openComplaints - 1 } : current);
      setToastTone("success");
      setToast(t(status === "resolved" ? "admin.complaintResolved" : "admin.complaintDismissed"));
    } catch { setToastTone("error"); setToast(t("admin.complaintFailed")); }
    finally { setClosing(null); }
  };

  if (denied) return <div className="screen screen--with-nav"><ErrorState onRetry={load} subtitle={t("ui.accessDeniedSubtitle")} title={t("ui.accessDenied")} /><BottomNav /></div>;
  if (failed && !dashboard) return <div className="screen screen--with-nav"><ErrorState onRetry={load} subtitle={t("common.connectionRetry")} title={t("common.openFailed")} /><BottomNav /></div>;
  if (!dashboard) return <div className="screen screen--with-nav"><LoadingState /><BottomNav /></div>;

  if (!section) {
    const tiles: Array<{ key: string; icon: string; value: number; label: string; href?: string; alert?: boolean }> = [
      { key: "users", icon: "user", value: dashboard.users, label: t("admin.users"), href: "#/admin/users" },
      { key: "bloggers", icon: "user", value: dashboard.bloggers, label: t("admin.bloggers"), href: "#/admin/bloggers" },
      { key: "businesses", icon: "building", value: dashboard.businesses, label: t("admin.businesses"), href: "#/admin/businesses" },
      { key: "campaigns", icon: "briefcase", value: dashboard.publishedCampaigns, label: t("admin.campaigns"), href: "#/admin/campaigns" },
      { key: "complaints", icon: "bell", value: dashboard.openComplaints ?? 0, label: t("admin.complaints"), href: "#/admin/complaints", alert: (dashboard.openComplaints ?? 0) > 0 },
      { key: "deals", icon: "check", value: dashboard.completedDeals, label: t("admin.completedDeals") }
    ];
    return <div className="screen screen--with-nav space-y-5 pt-5">
      <PageHeader back={{ href: "#/settings", label: t("common.back") }} eyebrow={t("common.appName")} title={t("admin.dashboardTitle")} />
      <section className="admin-tiles">
        {tiles.map((tile) => {
          const body = <><Icon className="admin-tile__icon" name={tile.icon} /><span className="admin-tile__value">{tile.value}</span><span className="admin-tile__label">{tile.label}</span></>;
          return tile.href
            ? <a className={`admin-tile admin-tile--link${tile.alert ? " admin-tile--alert" : ""}`} href={tile.href} key={tile.key}>{body}</a>
            : <div className="admin-tile" key={tile.key}>{body}</div>;
        })}
      </section>
      <Card className="admin-activity"><h2 className="text-sm font-extrabold">{t("admin.activityTitle")}</h2><a className="admin-activity__link mt-2 block text-sm" href="#/admin/users?filter=new">{t("admin.activityUsers", { today: dashboard.newUsersToday ?? 0, week: dashboard.newUsersWeek ?? 0 })} ›</a><p className="mt-1 text-sm text-brand-muted">{t("admin.activityDeals", { started: dashboard.newDealsWeek ?? 0, completed: dashboard.completedDealsWeek ?? 0 })}</p></Card>
      <a className="admin-log-link" href="#/admin/log"><span><strong>{t("admin.logTitle")}</strong><span>{t("admin.logHint")}</span></span><span aria-hidden="true">›</span></a>
      <Toast message={toast} tone={toastTone} />
      <BottomNav />
    </div>;
  }

  const usersById = new Map(lists.users.map((user) => [String(user.telegramUserId), user]));
  const titleKey = section === "log" ? "admin.logTitle" : section === "campaigns" ? "admin.campaigns" : `admin.${section}`;
  const empty = <Card><p className="text-sm text-brand-muted">{t(section === "complaints" ? "admin.complaintsEmpty" : "admin.listEmpty")}</p></Card>;
  const items = lists[section];

  return <div className="screen screen--with-nav space-y-4 pt-5">
    <PageHeader back={{ href: "#/admin", label: t("admin.dashboardTitle") }} title={t(titleKey)} />
    {section === "log" && <p className="text-sm leading-6 text-brand-muted">{t("admin.logHint")}</p>}
    {section === "users" && <div className="grid gap-3">
      <input aria-label={t("admin.userSearch")} className="ds-field w-full px-4" inputMode="search" onChange={(event) => setUserSearch(event.target.value)} placeholder={t("admin.userSearch")} type="search" value={userSearch} />
      <div aria-label={t("admin.userFilter")} className="admin-filters no-scrollbar" role="group">{userFilters.map((value) => <button aria-pressed={userFilter === value} className="admin-filter" key={value} onClick={() => setUserFilter(value)} type="button">{t(`admin.userFilter.${value}`)}</button>)}</div>
    </div>}
    {listLoading && items.length === 0 ? <LoadingState /> : items.length === 0 ? empty : <div className="grid gap-2">
      {section === "users" && lists.users.map((user) => {
        const profiles = [user.hasBloggerProfile && t("admin.userFilter.bloggers"), user.hasBusinessProfile && t("admin.userFilter.businesses"), user.hasBrandFaceProfile && t("admin.userFilter.brandFaces")].filter(Boolean).join(" · ");
        return <Card className="p-3" key={user.telegramUserId}>
          <div className="flex items-start justify-between gap-2"><div className="min-w-0"><p className="font-extrabold">{user.firstName}</p><p className="text-xs text-brand-muted">{user.username ? "@" + user.username : `id ${user.telegramUserId}`}{user.verifiedPhone ? ` · ${user.verifiedPhone}` : ""}</p>{profiles && <p className="mt-1 text-xs text-brand-muted">{profiles}</p>}<p className="mt-1 text-xs text-brand-muted">{t("admin.registeredAt", { date: formatDate(user.createdAtUtc) })}</p>{user.isDeleted && <p className="mt-1 text-xs text-brand-muted">{t("admin.deletedInfo", { date: formatDate(user.deletedAtUtc), actor: user.deletedByTelegramUserId ?? user.telegramUserId })}</p>}</div><Badge tone={user.isDeleted || user.isBlocked ? "gray" : "blue"}>{user.isDeleted ? t("profile.deleted") : user.isBlocked ? t("common.blocked") : roleLabels[user.role]}</Badge></div>
          {/* A chat opens only by @username: Telegram has no link to a person without one, so the phone is shown instead. */}
          {user.username && <Button className="mt-3 w-full" onClick={() => openLink(`https://t.me/${user.username}`)} type="button" variant="secondary">{t("admin.writeInTelegram")}</Button>}
          <div className="mt-2 flex gap-2"><select aria-label={t("admin.userRoleAria")} className="ds-legacy-field min-h-11 flex-1 rounded-xl border px-2 text-sm" disabled={user.isDeleted || user.role === 4} onChange={(event) => void updateRole(user.telegramUserId, Number(event.target.value))} value={user.role}>{roleLabels.map((label, value) => <option key={roleKeys[value]} value={value}>{label}</option>)}</select><Button disabled={user.isDeleted || user.role === 4} onClick={() => void toggleBlock(user)} type="button" variant={user.isBlocked ? "secondary" : "danger"}>{user.isBlocked ? t("common.unblock") : t("common.block")}</Button></div>
        </Card>;
      })}
      {section === "bloggers" && lists.bloggers.map((blogger) => <a className="admin-row" href={`#/blogger/${blogger.id}`} key={blogger.id}><Avatar name={blogger.name} size="sm" src={blogger.avatarUrl} /><span className="admin-row__text"><strong>{blogger.name}</strong><span>{t(`admin.moderation.${blogger.status}`)} · {formatDate(blogger.createdAtUtc)}</span></span></a>)}
      {section === "businesses" && lists.businesses.map((business) => <a className="admin-row" href={`#/company/${business.id}`} key={business.id}><Avatar name={business.name} size="sm" src={business.logoUrl} /><span className="admin-row__text"><strong>{business.name}</strong><span>{business.isDeleted ? t("common.deletedAccount") : t(`admin.moderation.${business.moderationStatus}`)} · {t("admin.businessCounts", { campaigns: business.campaignsCount, deals: business.completedDealsCount })}</span></span></a>)}
      {section === "campaigns" && lists.campaigns.map((campaign) => <a className="admin-row" href={`#/campaign/${campaign.id}`} key={campaign.id}><span className="admin-row__text"><strong>{campaign.title}</strong><span>{campaign.businessName} · {t(`admin.campaignStatus.${campaign.status}`)} · {t("admin.applicationsCount", { count: campaign.applicationsCount })}</span></span>{campaign.isPromoted && <Badge tone="gold">{t("campaign.promoted")}</Badge>}</a>)}
      {section === "complaints" && lists.complaints.map((complaint) => <Card className="p-3" key={complaint.id}>
        <div className="flex items-start justify-between gap-2"><div className="min-w-0"><p className="truncate font-extrabold">{complaint.targetName || t("common.deletedAccount")}</p><p className="text-xs text-brand-muted">{t(`admin.complaintTarget.${complaint.targetType}`)} · {formatDate(complaint.createdAtUtc)}</p></div><Badge tone="red">{t(`report.reason.${complaint.reason}`)}</Badge></div>
        {complaint.comment && <p className="mt-2 whitespace-pre-line text-sm leading-6 text-brand-muted">{complaint.comment}</p>}
        <p className="mt-2 text-xs text-brand-muted">{t("admin.complaintReporter", { name: complaint.reporterName || String(complaint.reporterTelegramUserId), id: complaint.reporterTelegramUserId })}</p>
        <div className="mt-3 grid grid-cols-2 gap-2"><Button disabled={closing !== null} onClick={() => void closeComplaint(complaint, "resolved")} type="button">{t("admin.complaintResolve")}</Button><Button disabled={closing !== null} onClick={() => void closeComplaint(complaint, "dismissed")} type="button" variant="secondary">{t("admin.complaintDismiss")}</Button></div>
      </Card>)}
      {section === "log" && lists.log.map((entry) => {
        const actor = usersById.get(String(entry.actorTelegramUserId));
        const target = entry.targetType === "PlatformUser" ? usersById.get(entry.targetId) : undefined;
        const known = t(`admin.log.${entry.action}`) !== `admin.log.${entry.action}`;
        return <Card className="p-3" key={entry.id}>
          <p className="text-sm font-bold">{known ? t(`admin.log.${entry.action}`) : entry.action}</p>
          <p className="mt-1 text-xs text-brand-muted">{actor?.firstName ?? entry.actorTelegramUserId}{target && target !== actor ? ` → ${target.firstName}` : ""} · {formatDate(entry.createdAtUtc)}</p>
        </Card>;
      })}
    </div>}
    <Toast message={toast} tone={toastTone} />
    <BottomNav />
  </div>;
}
