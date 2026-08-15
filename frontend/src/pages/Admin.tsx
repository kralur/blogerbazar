import { useCallback, useEffect, useMemo, useState } from "react";
import { getAdminAuditLogs, getAdminBloggers, getAdminDashboard, getAdminUsers, setAdminUserBlocked, updateAdminUserRole, type AdminAuditLog, type AdminBloggerProfile, type AdminDashboard, type AdminPlatformUser } from "../api/marketplace";
import { Avatar, Badge, BottomNav, Button, Card, ErrorState, LoadingState, StatsCard, Toast } from "../components/ui";
import { LanguageSwitcher } from "../components/LanguageSwitcher";
import { useI18n } from "../i18n";
import { formatDate } from "../lib/currency";
import { useProfileDataRefresh } from "../hooks/useProfileDataRefresh";

const roleKeys = ["admin.role.member", "admin.role.support", "admin.role.moderator", "admin.role.admin", "admin.role.owner"];

export function Admin() {
  const { t } = useI18n();
  const [dashboard, setDashboard] = useState<AdminDashboard | null>(null);
  const [users, setUsers] = useState<AdminPlatformUser[]>([]);
  const [bloggers, setBloggers] = useState<AdminBloggerProfile[]>([]);
  const [auditLogs, setAuditLogs] = useState<AdminAuditLog[]>([]);
  const [failed, setFailed] = useState(false);
  const [toast, setToast] = useState("");
  const [toastTone, setToastTone] = useState<"success" | "error">("success");
  const roleLabels = useMemo(() => roleKeys.map((key) => t(key)), [t]);
  const load = useCallback(() => {
    setFailed(false);
    Promise.all([getAdminDashboard(), getAdminUsers(), getAdminAuditLogs(), getAdminBloggers()])
      .then(([nextDashboard, nextUsers, nextLogs, nextBloggers]) => { setDashboard(nextDashboard); setUsers(nextUsers); setAuditLogs(nextLogs); setBloggers(nextBloggers); })
      .catch(() => setFailed(true));
  }, []);
  useEffect(load, []);
  useProfileDataRefresh(load);
  const updateRole = async (telegramUserId: number, role: number) => {
    try {
      const user = await updateAdminUserRole(telegramUserId, role);
      setUsers((items) => items.map((item) => item.telegramUserId === user.telegramUserId ? user : item));
      setToastTone("success");
      setToast(t("admin.roleUpdated"));
    } catch { setToastTone("error"); setToast(t("admin.roleUpdateDenied")); }
  };
  const toggleBlock = async (user: AdminPlatformUser) => {
    try {
      const updated = await setAdminUserBlocked(user.telegramUserId, !user.isBlocked);
      setUsers((items) => items.map((item) => item.telegramUserId === updated.telegramUserId ? updated : item));
      setToastTone("success");
      setToast(updated.isBlocked ? t("admin.userBlocked") : t("admin.userUnblocked"));
    } catch { setToastTone("error"); setToast(t("admin.userUpdateDenied")); }
  };
  if (!dashboard && !failed) return <div className="screen screen--with-nav"><LoadingState /><BottomNav /></div>;
  if (failed || !dashboard) return <div className="screen screen--with-nav"><ErrorState onRetry={load} subtitle={t("ui.accessDeniedSubtitle")} title={t("ui.accessDenied")} /><BottomNav /></div>;
  return <div className="screen screen--with-nav space-y-5 pt-5">
    <header className="flex items-start justify-between gap-3"><div><p className="text-sm font-semibold text-brand-muted">{t("common.appName")}</p><h1 className="mt-1 text-3xl font-extrabold tracking-tight">{t("admin.dashboardTitle")}</h1></div><LanguageSwitcher /></header>
    <Card className="border-blue-100 bg-gradient-to-br from-blue-50 via-white to-cyan-50"><p className="text-sm leading-6 text-brand-muted">{t("admin.dashboardDescription")}</p></Card>
    <section className="grid grid-cols-2 gap-3"><StatsCard icon="user" label={t("admin.users")} value={String(dashboard.users)} /><StatsCard icon="user" label={t("admin.bloggers")} value={String(dashboard.bloggers)} /><StatsCard icon="building" label={t("admin.businesses")} value={String(dashboard.businesses)} /><StatsCard icon="briefcase" label={t("common.campaigns", { count: dashboard.publishedCampaigns })} value={String(dashboard.publishedCampaigns)} /><StatsCard icon="check" label={t("common.deals", { count: dashboard.completedDeals })} value={String(dashboard.completedDeals)} /><StatsCard icon="star" label={t("admin.promotions")} value={String(dashboard.promotedBloggers + dashboard.promotedCampaigns)} /></section>
    <section><h2 className="mb-3 text-lg font-extrabold">{t("admin.users")}</h2><div className="grid gap-2">{users.map((user) => <Card className="p-3" key={user.telegramUserId}><div className="flex items-start justify-between gap-2"><div><p className="font-extrabold">{user.firstName}</p><p className="text-xs text-brand-muted">{user.username ? "@" + user.username : user.telegramUserId}</p>{user.isDeleted && <p className="mt-1 text-xs text-brand-muted">{t("admin.deletedInfo", { date: formatDate(user.deletedAtUtc), actor: user.deletedByTelegramUserId ?? user.telegramUserId })}</p>}</div><Badge tone={user.isDeleted || user.isBlocked ? "gray" : "blue"}>{user.isDeleted ? t("profile.deleted") : user.isBlocked ? t("common.blocked") : roleLabels[user.role]}</Badge></div><div className="mt-3 flex gap-2"><select aria-label={t("admin.userRoleAria")} className="min-h-11 flex-1 rounded-xl border border-brand-line bg-white px-2 text-sm" disabled={user.isDeleted || user.role === 4} onChange={(event) => void updateRole(user.telegramUserId, Number(event.target.value))} value={user.role}>{roleLabels.map((label, value) => <option key={roleKeys[value]} value={value}>{label}</option>)}</select><Button disabled={user.isDeleted || user.role === 4} onClick={() => void toggleBlock(user)} type="button" variant={user.isBlocked ? "secondary" : "danger"}>{user.isBlocked ? t("common.unblock") : t("common.block")}</Button></div></Card>)}</div></section>
    <section><h2 className="mb-3 text-lg font-extrabold">{t("admin.bloggers")}</h2><div className="grid gap-2">{bloggers.map((blogger) => <Card className="flex items-center gap-3 p-3" key={blogger.id}><Avatar name={blogger.name} size="sm" src={blogger.avatarUrl} /><div className="min-w-0"><p className="truncate font-extrabold">{blogger.name}</p><p className="mt-1 truncate text-xs text-brand-muted">{blogger.city}</p></div></Card>)}</div></section>
    <section><h2 className="mb-3 text-lg font-extrabold">{t("common.actionLog")}</h2><div className="grid gap-2">{auditLogs.length ? auditLogs.slice(0, 12).map((entry) => <Card className="p-3" key={entry.id}><p className="text-sm font-bold">{entry.action}</p><p className="mt-1 text-xs text-brand-muted">{entry.targetType} · {entry.targetId}</p></Card>) : <Card><p className="text-sm text-brand-muted">{t("common.noActions")}</p></Card>}</div></section>
    <BottomNav /><Toast message={toast} tone={toastTone} />
  </div>;
}
