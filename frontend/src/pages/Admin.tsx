import { useCallback, useEffect, useRef, useState } from "react";
import { ApiError } from "../api/client";
import { closeAdminComplaint, getAdminBloggers, getAdminBusinesses, getAdminComplaints, getAdminDashboard, peekAdminDashboard, sendAdminTipPreview, setAdminUserBlocked, type AdminBloggerProfile, type AdminBusiness, type AdminComplaint, type AdminComplaintStatus, type AdminDashboard } from "../api/marketplace";
import { Avatar, BottomNav, Button, Card, ErrorState, Icon, LoadingState, Toast } from "../components/ui";
import { useI18n } from "../i18n";
import { formatDate } from "../lib/currency";
import { useProfileDataRefresh } from "../hooks/useProfileDataRefresh";
import { PageHeader } from "../components/PageHeader";
import { useTelegram } from "../telegram/TelegramProvider";
import { adminUserCardHref } from "./AdminUserCard";

// The Mini App keeps a pocket admin for quick work from the phone (complaints, moderation, blocking);
// lists, analytics, the funnel, the journal and security live on the admin site (D57).
const sections = ["bloggers", "businesses", "complaints"] as const;
type Section = typeof sections[number];
const isSection = (value?: string): value is Section => Boolean(value && (sections as readonly string[]).includes(value));

type Lists = { bloggers: AdminBloggerProfile[]; businesses: AdminBusiness[]; complaints: AdminComplaint[] };
const emptyLists: Lists = { bloggers: [], businesses: [], complaints: [] };
const complaintStatuses: AdminComplaintStatus[] = ["open", "resolved", "dismissed"];
// Public things open from the case; offers and deals are private to their two sides, so they have no link.
const complaintTargetRoutes: Partial<Record<AdminComplaint["targetType"], string>> = { blogger: "#/blogger/", brandFace: "#/brand-face-detail/", business: "#/company/", campaign: "#/campaign/" };

// The admin home is a grid of tiles; each tile opens its own list at #/admin/<section>, so nothing needs long scrolling.
export function Admin({ section: rawSection }: { section?: string }) {
  const { t } = useI18n();
  const section = isSection(rawSection) ? rawSection : null;
  const [dashboard, setDashboard] = useState<AdminDashboard | null>(peekAdminDashboard);
  const [lists, setLists] = useState<Lists>(emptyLists);
  const [listLoading, setListLoading] = useState(false);
  const [closing, setClosing] = useState<string | null>(null);
  const [failed, setFailed] = useState(false);
  // Only a refused dashboard means "no access"; any other failure is a load error.
  const [denied, setDenied] = useState(false);
  const [toast, setToast] = useState("");
  const [toastTone, setToastTone] = useState<"success" | "error">("success");
  const { openLink } = useTelegram();
  const [complaintStatus, setComplaintStatus] = useState<AdminComplaintStatus>("open");
  const complaintQuery = useRef<AdminComplaintStatus>("open");
  complaintQuery.current = complaintStatus;

  const loadSection = useCallback(async (target: Section | null) => {
    if (!target) return;
    setListLoading(true);
    try {
      if (target === "bloggers") { const bloggers = await getAdminBloggers(); setLists((current) => ({ ...current, bloggers })); }
      if (target === "businesses") { const businesses = await getAdminBusinesses(); setLists((current) => ({ ...current, businesses })); }
      if (target === "complaints") { const complaints = await getAdminComplaints(complaintQuery.current); setLists((current) => ({ ...current, complaints })); }
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
  // A status chip asks the server again; the first visit is loaded by load() above.
  const complaintStatusShown = useRef<AdminComplaintStatus>("open");
  useEffect(() => {
    if (section !== "complaints" || complaintStatusShown.current === complaintStatus) return;
    complaintStatusShown.current = complaintStatus;
    setLists((current) => ({ ...current, complaints: [] }));
    void loadSection("complaints");
  }, [complaintStatus, loadSection, section]);

  // Blocking from a case: only the owner may block (the server checks), and every case of the same person shows the new state.
  const toggleOwnerBlock = async (complaint: AdminComplaint) => {
    if (!complaint.ownerTelegramUserId) return;
    try {
      const updated = await setAdminUserBlocked(complaint.ownerTelegramUserId, !complaint.ownerIsBlocked);
      setLists((current) => ({ ...current, complaints: current.complaints.map((item) => item.ownerTelegramUserId === updated.telegramUserId ? { ...item, ownerIsBlocked: updated.isBlocked } : item) }));
      setToastTone("success");
      setToast(updated.isBlocked ? t("admin.userBlocked") : t("admin.userUnblocked"));
    } catch { setToastTone("error"); setToast(t("admin.userUpdateDenied")); }
  };
  const [tipSending, setTipSending] = useState(false);
  const previewTip = async () => {
    if (tipSending) return;
    setTipSending(true);
    try {
      const result = await sendAdminTipPreview();
      setToastTone(result.sent ? "success" : "error");
      setToast(result.sent ? t("admin.tipSent", { number: result.number, total: result.total }) : t("admin.tipFailed"));
    } catch { setToastTone("error"); setToast(t("admin.tipFailed")); }
    finally { setTipSending(false); }
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

  const adminWebUrl = dashboard.adminWebUrl ?? null;
  if (!section) {
    const tiles: Array<{ key: string; icon: string; value: number; label: string; href?: string; alert?: boolean }> = [
      { key: "users", icon: "user", value: dashboard.users, label: t("admin.users") },
      { key: "bloggers", icon: "user", value: dashboard.bloggers, label: t("admin.bloggers"), href: "#/admin/bloggers" },
      { key: "businesses", icon: "building", value: dashboard.businesses, label: t("admin.businesses"), href: "#/admin/businesses" },
      { key: "campaigns", icon: "briefcase", value: dashboard.publishedCampaigns, label: t("admin.campaigns") },
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
      <Card className="admin-activity"><h2 className="text-sm font-extrabold">{t("admin.activityTitle")}</h2><p className="mt-2 text-sm">{t("admin.activityUsers", { today: dashboard.newUsersToday ?? 0, week: dashboard.newUsersWeek ?? 0 })}</p><p className="mt-1 text-sm text-brand-muted">{t("admin.activityActive", { today: dashboard.activeToday ?? 0, week: dashboard.activeWeek ?? 0 })}</p><p className="mt-1 text-sm text-brand-muted">{t("admin.activityDeals", { started: dashboard.newDealsWeek ?? 0, completed: dashboard.completedDealsWeek ?? 0 })}</p></Card>
      <Card className="admin-activity"><h2 className="text-sm font-extrabold">{t("admin.tipsTitle")}</h2><p className="mt-1 text-sm text-brand-muted">{t("admin.tipsHint")}</p><Button className="mt-3 w-full" disabled={tipSending} onClick={() => void previewTip()} type="button" variant="secondary">{t("admin.tipPreview")}</Button></Card>
      {/* Lists, analytics, the funnel, the journal and security are on the admin site; it opens in the browser. */}
      {adminWebUrl && <a className="admin-log-link" href={adminWebUrl} onClick={(event) => { event.preventDefault(); openLink(adminWebUrl); }} rel="noreferrer" target="_blank"><span><strong>{t("admin.siteTitle")}</strong><span>{t("admin.siteHint")}</span></span><span aria-hidden="true">›</span></a>}
      <Toast message={toast} tone={toastTone} />
      <BottomNav />
    </div>;
  }

  const titleKey = `admin.${section}`;
  const empty = <Card><p className="text-sm text-brand-muted">{t(section === "complaints" && complaintStatus === "open" ? "admin.complaintsEmpty" : "admin.listEmpty")}</p></Card>;
  const items = lists[section];

  return <div className="screen screen--with-nav space-y-4 pt-5">
    <PageHeader back={{ href: "#/admin", label: t("admin.dashboardTitle") }} title={t(titleKey)} />
    {section === "complaints" && <div aria-label={t("admin.complaintStatusFilter")} className="admin-filters no-scrollbar" role="group">{complaintStatuses.map((value) => <button aria-pressed={complaintStatus === value} className="admin-filter" key={value} onClick={() => setComplaintStatus(value)} type="button">{t(`admin.complaintStatus.${value}`)}</button>)}</div>}
    {listLoading && items.length === 0 ? <LoadingState /> : items.length === 0 ? empty : <div className="grid grid-cols-1 gap-2">
      {section === "bloggers" && lists.bloggers.map((blogger) => <a className="admin-row" href={`#/blogger/${blogger.id}`} key={blogger.id}><Avatar name={blogger.name} size="sm" src={blogger.avatarUrl} /><span className="admin-row__text"><strong>{blogger.name}</strong><span>{t(`admin.moderation.${blogger.status}`)} · {formatDate(blogger.createdAtUtc)}</span></span></a>)}
      {section === "businesses" && lists.businesses.map((business) => <a className="admin-row" href={`#/company/${business.id}`} key={business.id}><Avatar name={business.name} size="sm" src={business.logoUrl} /><span className="admin-row__text"><strong>{business.name}</strong><span>{business.isDeleted ? t("common.deletedAccount") : t(`admin.moderation.${business.moderationStatus}`)} · {t("admin.businessCounts", { campaigns: business.campaignsCount, deals: business.completedDealsCount })}</span></span></a>)}
      {section === "complaints" && lists.complaints.map((complaint) => <Card className="p-3" key={complaint.id}>
        {/* The reason gets its own line: long reasons ("Spam or ...") must wrap, not push the card off the screen. */}
        <p className="truncate font-extrabold">{complaint.targetName || t("common.deletedAccount")}</p>
        <p className="text-xs text-brand-muted">{t(`admin.complaintTarget.${complaint.targetType}`)} · {formatDate(complaint.createdAtUtc)}</p>
        <p className="admin-complaint__reason mt-2">{t(`report.reason.${complaint.reason}`)}</p>
        {complaint.comment && <p className="mt-2 whitespace-pre-line text-sm leading-6 text-brand-muted">{complaint.comment}</p>}
        <a className="mt-2 block text-xs text-brand-muted" href={adminUserCardHref(complaint.reporterTelegramUserId)}>{t("admin.complaintReporter", { name: complaint.reporterName || String(complaint.reporterTelegramUserId), id: complaint.reporterTelegramUserId })} ›</a>
        {complaint.status !== "open" && <p className="mt-1 text-xs text-brand-muted">{t("admin.complaintClosedAt", { date: formatDate(complaint.resolvedAtUtc) })}</p>}
        {(complaintTargetRoutes[complaint.targetType] || complaint.ownerTelegramUserId) && <div className="mt-3 flex flex-wrap gap-2">
          {complaintTargetRoutes[complaint.targetType] && <a className="ds-button ds-button--secondary tap-target inline-flex flex-auto items-center justify-center px-5" href={`${complaintTargetRoutes[complaint.targetType]}${complaint.targetId}`}>{t("admin.complaintOpenTarget")}</a>}
          {complaint.ownerTelegramUserId && !complaint.ownerIsPlatformOwner && <Button className="flex-auto" onClick={() => void toggleOwnerBlock(complaint)} type="button" variant={complaint.ownerIsBlocked ? "secondary" : "danger"}>{t(complaint.ownerIsBlocked ? "common.unblock" : "common.block")}</Button>}
        </div>}
        {complaint.status === "open" && <div className="mt-2 grid grid-cols-2 gap-2"><Button disabled={closing !== null} onClick={() => void closeComplaint(complaint, "resolved")} type="button">{t("admin.complaintResolve")}</Button><Button disabled={closing !== null} onClick={() => void closeComplaint(complaint, "dismissed")} type="button" variant="secondary">{t("admin.complaintDismiss")}</Button></div>}
      </Card>)}
    </div>}
    <Toast message={toast} tone={toastTone} />
    <BottomNav />
  </div>;
}
