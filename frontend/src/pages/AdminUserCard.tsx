import { useCallback, useEffect, useState } from "react";
import { ApiError } from "../api/client";
import { getAdminUserDetails, setAdminUserBlocked, type AdminUserDetails, type AdminUserProfile } from "../api/marketplace";
import { PageHeader } from "../components/PageHeader";
import { Badge, BottomNav, Button, Card, ErrorState, LoadingState, Toast } from "../components/ui";
import { useI18n } from "../i18n";
import { formatCurrency, formatDate } from "../lib/currency";
import { useTelegram } from "../telegram/TelegramProvider";

const profileRoutes: Record<AdminUserProfile["role"], string> = { blogger: "#/blogger/", brandFace: "#/brand-face-detail/", business: "#/company/" };
const dealStatusKeys = ["requests.dealActive", "requests.dealCompleted", "requests.dealCancelled"];

export const adminUserCardHref = (telegramUserId: number) => `#/admin/user?id=${telegramUserId}`;

function idFromHash() {
  const value = Number(new URLSearchParams(window.location.hash.split("?")[1] ?? "").get("id"));
  return Number.isSafeInteger(value) && value > 0 ? value : null;
}

// Admin user card (#/admin/user?id=<telegram id>): profiles, deals and complaints of one person on one screen.
export function AdminUserCard() {
  const { t } = useI18n();
  const { openLink } = useTelegram();
  const [telegramUserId] = useState(idFromHash);
  const [details, setDetails] = useState<AdminUserDetails | null>(null);
  const [state, setState] = useState<"loading" | "ready" | "denied" | "failed">("loading");
  const [toast, setToast] = useState("");
  const [toastTone, setToastTone] = useState<"success" | "error">("success");

  const load = useCallback(() => {
    if (!telegramUserId) { setState("failed"); return; }
    setState("loading");
    getAdminUserDetails(telegramUserId)
      .then((result) => { setDetails(result); setState("ready"); })
      .catch((error) => setState(error instanceof ApiError && (error.status === 401 || error.status === 403) ? "denied" : "failed"));
  }, [telegramUserId]);
  useEffect(load, [load]);

  const toggleBlock = async () => {
    if (!details) return;
    try {
      const user = await setAdminUserBlocked(details.user.telegramUserId, !details.user.isBlocked);
      setDetails({ ...details, user });
      setToastTone("success");
      setToast(user.isBlocked ? t("admin.userBlocked") : t("admin.userUnblocked"));
    } catch { setToastTone("error"); setToast(t("admin.userUpdateDenied")); }
  };

  const header = <PageHeader back={{ href: "#/admin/users", label: t("admin.users") }} title={t("admin.userCard")} />;
  if (state === "denied") return <div className="screen screen--with-nav space-y-4 pt-5">{header}<ErrorState onRetry={load} subtitle={t("ui.accessDeniedSubtitle")} title={t("ui.accessDenied")} /><BottomNav /></div>;
  if (state === "failed") return <div className="screen screen--with-nav space-y-4 pt-5">{header}<ErrorState onRetry={load} subtitle={t("common.connectionRetry")} title={t("common.openFailed")} /><BottomNav /></div>;
  if (state === "loading" || !details) return <div className="screen screen--with-nav space-y-4 pt-5">{header}<LoadingState /><BottomNav /></div>;

  const { user } = details;
  return <div className="screen screen--with-nav space-y-4 pt-5">
    {header}
    <Card className="p-4">
      <div className="flex items-start justify-between gap-2">
        <div className="min-w-0">
          <p className="text-lg font-extrabold">{user.firstName}</p>
          <p className="text-sm text-brand-muted">{user.username ? "@" + user.username : `id ${user.telegramUserId}`}{user.verifiedPhone ? ` · ${user.verifiedPhone}` : ""}</p>
          <p className="mt-1 text-xs text-brand-muted">{t("admin.registeredAt", { date: formatDate(user.createdAtUtc) })} · id {user.telegramUserId}</p>
        </div>
        {(user.isBlocked || user.isDeleted) && <Badge tone="gray">{user.isDeleted ? t("profile.deleted") : t("common.blocked")}</Badge>}
      </div>
      <div className="mt-3 flex flex-wrap gap-2">
        {user.username && <Button className="flex-auto" onClick={() => openLink(`https://t.me/${user.username}`)} type="button" variant="secondary">{t("admin.writeInTelegram")}</Button>}
        {!user.isDeleted && user.role !== 4 && <Button className="flex-auto" onClick={() => void toggleBlock()} type="button" variant={user.isBlocked ? "secondary" : "danger"}>{user.isBlocked ? t("common.unblock") : t("common.block")}</Button>}
      </div>
    </Card>

    <Card className="p-4">
      <h2 className="text-sm font-extrabold">{t("admin.userProfiles")}</h2>
      {details.profiles.length === 0 ? <p className="mt-2 text-sm text-brand-muted">{t("admin.userNoProfiles")}</p> : <div className="mt-2 grid grid-cols-1 gap-2">
        {details.profiles.map((profile) => <a className="admin-row" href={`${profileRoutes[profile.role]}${profile.id}`} key={profile.role}>
          <span className="admin-row__text"><strong>{profile.name}</strong><span>{t(`admin.userProfileRole.${profile.role}`)}{profile.isDeleted ? ` · ${t("admin.userProfileDeleted")}` : profile.isHidden ? ` · ${t("admin.userProfileHidden")}` : ""}</span></span>
        </a>)}
      </div>}
    </Card>

    <Card className="p-4">
      <h2 className="text-sm font-extrabold">{t("admin.userDeals")}</h2>
      <p className="mt-1 text-sm text-brand-muted">{t("admin.userDealCounts", { active: details.activeDeals, completed: details.completedDeals, cancelled: details.cancelledDeals })}</p>
      {details.recentDeals.length === 0 ? <p className="mt-2 text-sm text-brand-muted">{t("admin.userNoDeals")}</p> : <div className="mt-2 grid grid-cols-1 gap-2">
        {details.recentDeals.map((deal) => <div className="admin-row" key={deal.id}>
          <span className="admin-row__text"><strong>{deal.partnerName || t("common.deletedAccount")}</strong><span>{[deal.title, t(dealStatusKeys[deal.status] ?? "requests.dealActive"), deal.agreedPrice != null ? formatCurrency(deal.agreedPrice) : null, formatDate(deal.createdAtUtc)].filter(Boolean).join(" · ")}</span></span>
        </div>)}
      </div>}
    </Card>

    <Card className="p-4">
      <h2 className="text-sm font-extrabold">{t("admin.complaints")}</h2>
      <p className="mt-1 text-sm text-brand-muted">{t("admin.userComplaintCounts", { against: details.complaintsAgainst, open: details.openComplaintsAgainst, filed: details.complaintsFiled })}</p>
      {details.openComplaintsAgainst > 0 && <a className="admin-activity__link mt-2 block text-sm" href="#/admin/complaints">{t("admin.complaints")} ›</a>}
    </Card>
    <Toast message={toast} tone={toastTone} />
    <BottomNav />
  </div>;
}
