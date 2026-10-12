import { useCallback, useEffect, useMemo, useState } from "react";
import { ApiError } from "../api/client";
import { closeAdminComplaint, getAdminAuditLogs, getAdminBloggers, getAdminComplaints, getAdminDashboard, getAdminUsers, setAdminUserBlocked, updateAdminUserRole, type AdminAuditLog, type AdminBloggerProfile, type AdminComplaint, type AdminDashboard, type AdminPlatformUser } from "../api/marketplace";
import { Avatar, Badge, BottomNav, Button, Card, ErrorState, LoadingState, StatsCard, Toast } from "../components/ui";
import { useI18n } from "../i18n";
import { formatDate } from "../lib/currency";
import { useProfileDataRefresh } from "../hooks/useProfileDataRefresh";
import { PageHeader } from "../components/PageHeader";

const roleKeys = ["admin.role.member", "admin.role.support", "admin.role.moderator", "admin.role.admin", "admin.role.owner"];

export function Admin() {
  const { t } = useI18n();
  const [dashboard, setDashboard] = useState<AdminDashboard | null>(null);
  const [users, setUsers] = useState<AdminPlatformUser[]>([]);
  const [bloggers, setBloggers] = useState<AdminBloggerProfile[]>([]);
  const [auditLogs, setAuditLogs] = useState<AdminAuditLog[]>([]);
  const [complaints, setComplaints] = useState<AdminComplaint[]>([]);
  const [closing, setClosing] = useState<string | null>(null);
  const [failed, setFailed] = useState(false);
  const [toast, setToast] = useState("");
  const [toastTone, setToastTone] = useState<"success" | "error">("success");
  const roleLabels = useMemo(() => roleKeys.map((key) => t(key)), [t]);
  // Only a refused dashboard means "no access"; any other failure is a load error, and sections that did load still show.
  const [denied, setDenied] = useState(false);
  const load = useCallback(() => {
    setFailed(false);
    setDenied(false);
    void Promise.allSettled([getAdminDashboard(), getAdminUsers(), getAdminAuditLogs(), getAdminBloggers(), getAdminComplaints()])
      .then(([nextDashboard, nextUsers, nextLogs, nextBloggers, nextComplaints]) => {
        if (nextDashboard.status === "fulfilled") setDashboard(nextDashboard.value);
        else if (nextDashboard.reason instanceof ApiError && (nextDashboard.reason.status === 401 || nextDashboard.reason.status === 403)) setDenied(true);
        else setFailed(true);
        if (nextUsers.status === "fulfilled") setUsers(nextUsers.value);
        if (nextLogs.status === "fulfilled") setAuditLogs(nextLogs.value);
        if (nextBloggers.status === "fulfilled") setBloggers(nextBloggers.value);
        setComplaints(nextComplaints.status === "fulfilled" ? nextComplaints.value : []);
      });
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
  const closeComplaint = async (complaint: AdminComplaint, status: "resolved" | "dismissed") => {
    if (closing) return;
    setClosing(complaint.id);
    try {
      await closeAdminComplaint(complaint.id, status);
      setComplaints((items) => items.filter((item) => item.id !== complaint.id));
      setDashboard((current) => current && current.openComplaints ? { ...current, openComplaints: current.openComplaints - 1 } : current);
      setToastTone("success");
      setToast(t(status === "resolved" ? "admin.complaintResolved" : "admin.complaintDismissed"));
    } catch { setToastTone("error"); setToast(t("admin.complaintFailed")); }
    finally { setClosing(null); }
  };
  if (!dashboard && !failed && !denied) return <div className="screen screen--with-nav"><LoadingState /><BottomNav /></div>;
  if (denied) return <div className="screen screen--with-nav"><ErrorState onRetry={load} subtitle={t("ui.accessDeniedSubtitle")} title={t("ui.accessDenied")} /><BottomNav /></div>;
  if (failed || !dashboard) return <div className="screen screen--with-nav"><ErrorState onRetry={load} subtitle={t("common.connectionRetry")} title={t("common.openFailed")} /><BottomNav /></div>;
  return <div className="screen screen--with-nav space-y-5 pt-5">
    <PageHeader eyebrow={t("common.appName")} title={t("admin.dashboardTitle")} />
    <Card><p className="text-sm leading-6 text-brand-muted">{t("admin.dashboardDescription")}</p></Card>
    <section className="grid grid-cols-2 gap-3"><StatsCard icon="user" label={t("admin.users")} value={String(dashboard.users)} /><StatsCard icon="user" label={t("admin.bloggers")} value={String(dashboard.bloggers)} /><StatsCard icon="building" label={t("admin.businesses")} value={String(dashboard.businesses)} /><StatsCard icon="briefcase" label={t("common.campaigns", { count: dashboard.publishedCampaigns })} value={String(dashboard.publishedCampaigns)} /><StatsCard icon="check" label={t("common.deals", { count: dashboard.completedDeals })} value={String(dashboard.completedDeals)} /><StatsCard icon="star" label={t("admin.promotions")} value={String(dashboard.promotedBloggers + dashboard.promotedCampaigns)} /></section>
    <section><h2 className="mb-3 text-lg font-extrabold">{t("admin.complaints")}{dashboard.openComplaints ? ` · ${dashboard.openComplaints}` : ""}</h2><div className="grid gap-2">{complaints.length ? complaints.map((complaint) => <Card className="p-3" key={complaint.id}>
      <div className="flex items-start justify-between gap-2"><div className="min-w-0"><p className="truncate font-extrabold">{complaint.targetName || t("common.deletedAccount")}</p><p className="text-xs text-brand-muted">{t(`admin.complaintTarget.${complaint.targetType}`)} · {formatDate(complaint.createdAtUtc)}</p></div><Badge tone="red">{t(`report.reason.${complaint.reason}`)}</Badge></div>
      {complaint.comment && <p className="mt-2 whitespace-pre-line text-sm leading-6 text-brand-muted">{complaint.comment}</p>}
      <p className="mt-2 text-xs text-brand-muted">{t("admin.complaintReporter", { name: complaint.reporterName || String(complaint.reporterTelegramUserId), id: complaint.reporterTelegramUserId })}</p>
      <div className="mt-3 grid grid-cols-2 gap-2"><Button disabled={closing !== null} onClick={() => void closeComplaint(complaint, "resolved")} type="button">{t("admin.complaintResolve")}</Button><Button disabled={closing !== null} onClick={() => void closeComplaint(complaint, "dismissed")} type="button" variant="secondary">{t("admin.complaintDismiss")}</Button></div>
    </Card>) : <Card><p className="text-sm text-brand-muted">{t("admin.complaintsEmpty")}</p></Card>}</div></section>
    <section><h2 className="mb-3 text-lg font-extrabold">{t("admin.users")}</h2><div className="grid gap-2">{users.map((user) => <Card className="p-3" key={user.telegramUserId}><div className="flex items-start justify-between gap-2"><div><p className="font-extrabold">{user.firstName}</p><p className="text-xs text-brand-muted">{user.username ? "@" + user.username : user.telegramUserId}</p>{user.isDeleted && <p className="mt-1 text-xs text-brand-muted">{t("admin.deletedInfo", { date: formatDate(user.deletedAtUtc), actor: user.deletedByTelegramUserId ?? user.telegramUserId })}</p>}</div><Badge tone={user.isDeleted || user.isBlocked ? "gray" : "blue"}>{user.isDeleted ? t("profile.deleted") : user.isBlocked ? t("common.blocked") : roleLabels[user.role]}</Badge></div><div className="mt-3 flex gap-2"><select aria-label={t("admin.userRoleAria")} className="ds-legacy-field min-h-11 flex-1 rounded-xl border px-2 text-sm" disabled={user.isDeleted || user.role === 4} onChange={(event) => void updateRole(user.telegramUserId, Number(event.target.value))} value={user.role}>{roleLabels.map((label, value) => <option key={roleKeys[value]} value={value}>{label}</option>)}</select><Button disabled={user.isDeleted || user.role === 4} onClick={() => void toggleBlock(user)} type="button" variant={user.isBlocked ? "secondary" : "danger"}>{user.isBlocked ? t("common.unblock") : t("common.block")}</Button></div></Card>)}</div></section>
    <section><h2 className="mb-3 text-lg font-extrabold">{t("admin.bloggers")}</h2><div className="grid gap-2">{bloggers.map((blogger) => <Card className="flex items-center gap-3 p-3" key={blogger.id}><Avatar name={blogger.name} size="sm" src={blogger.avatarUrl} /><div className="min-w-0"><p className="truncate font-extrabold">{blogger.name}</p><p className="mt-1 truncate text-xs text-brand-muted">{blogger.city}</p></div></Card>)}</div></section>
    <section><h2 className="mb-3 text-lg font-extrabold">{t("common.actionLog")}</h2><div className="grid gap-2">{auditLogs.length ? auditLogs.slice(0, 12).map((entry) => <Card className="p-3" key={entry.id}><p className="text-sm font-bold">{entry.action}</p><p className="mt-1 text-xs text-brand-muted">{entry.targetType} · {entry.targetId}</p></Card>) : <Card><p className="text-sm text-brand-muted">{t("common.noActions")}</p></Card>}</div></section>
    <BottomNav /><Toast message={toast} tone={toastTone} />
  </div>;
}
