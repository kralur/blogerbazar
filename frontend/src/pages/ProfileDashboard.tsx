import { useCallback, useEffect, useState } from "react";
import { deleteCurrentAccount, deleteProfileImage, getCurrentPlatformUser, getMyBloggerProfile, getMyBrandFaceProfile, getMyBusinessProfile, getMyCampaignApplications, getMyDeals, normalizeMarketplaceRole, selectMarketplaceRole, uploadProfileImage, type MarketplaceRole, type MyBloggerProfile, type MyBrandFaceProfile, type MyBusinessProfile, type ProfileMediaTarget } from "../api/marketplace";
import { ApiError, getApiErrorMessage } from "../api/client";
import { Badge, BottomNav, Button, Card, EmptyState, ErrorState, Icon, Modal, Skeleton, StatsCard, Toast } from "../components/ui";
import { LanguageSwitcher } from "../components/LanguageSwitcher";
import { useI18n } from "../i18n";
import { useTelegram } from "../telegram/TelegramProvider";
import { useFavorites } from "../features/favorites/FavoritesProvider";
import { useScrollRestoration } from "../hooks/useScrollRestoration";
import { ProfileMediaPicker, type PendingProfileImage } from "../components/ProfileMediaPicker";
import { notifyProfileDataChanged, useProfileDataRefresh } from "../hooks/useProfileDataRefresh";

type SelectedRole = "blogger" | "brandFace" | "business";
const selectedRoleKey = "bloggerbazar.selectedRole";

function bloggerStatus(status: number | undefined, t: (key: string) => string) {
  if (status === 1) return { label: t("profile.approved"), tone: "green" as const };
  if (status === 2) return { label: t("profile.rejected"), tone: "gray" as const };
  if (status === 4) return { label: t("profile.needsChanges"), tone: "orange" as const };
  return { label: t("profile.pending"), tone: "gold" as const };
}

export function ProfileDashboard({ onSessionReset, onMarketplaceRoleSelected }: { onSessionReset?: () => void; onMarketplaceRoleSelected?: (role: MarketplaceRole) => void }) {
  const { haptic, user: telegramUser } = useTelegram();
  useScrollRestoration("profile");
  const [blogger, setBlogger] = useState<MyBloggerProfile | null>(null);
  const [brandFace, setBrandFace] = useState<MyBrandFaceProfile | null>(null);
  const [business, setBusiness] = useState<MyBusinessProfile | null>(null);
  const [loading, setLoading] = useState(true);
  const [loadFailed, setLoadFailed] = useState(false);
  const [logoutOpen, setLogoutOpen] = useState(false);
  const [deleteOpen, setDeleteOpen] = useState(false);
  const [deleting, setDeleting] = useState(false);
  const [toast, setToast] = useState("");
  const [toastTone, setToastTone] = useState<"success" | "error">("error");
  const [accountImagePending, setAccountImagePending] = useState<PendingProfileImage>();
  const [accountImageSaving, setAccountImageSaving] = useState(false);
  const [switchingRole, setSwitchingRole] = useState(false);
  const [applicationsCount, setApplicationsCount] = useState(0);
  const [dealsCount, setDealsCount] = useState(0);
  const [role, setRole] = useState<SelectedRole>(() => {
    const savedRole = localStorage.getItem(selectedRoleKey);
    return savedRole === "business" || savedRole === "brandFace" ? savedRole : "blogger";
  });
  const { language, setLanguage, t: translate } = useI18n();
  const t = (key: string, values?: Record<string, string | number>) => translate(
    key === "profile.requestsAndDeals" ? "requests.title" : key === "profile.requestsAndDealsSubtitle" ? "requests.emptyApplicationsSubtitle" : key,
    values
  );
  const { refreshFavorites } = useFavorites();

  const loadDashboard = useCallback(() => {
    let cancelled = false;
    setLoading(true);
    setLoadFailed(false);
    Promise.allSettled([getCurrentPlatformUser(), getMyBloggerProfile(), getMyBrandFaceProfile(), getMyBusinessProfile(), getMyCampaignApplications(), getMyDeals()]).then(([userResult, bloggerResult, brandFaceResult, businessResult, applicationsResult, dealsResult]) => {
      if (cancelled) return;
      const profileResults = [bloggerResult, brandFaceResult, businessResult];
      const hasUnexpectedProfileFailure = profileResults.some((result) => result.status === "rejected" && (!(result.reason instanceof ApiError) || result.reason.status !== 404));
      if (userResult.status === "rejected" || hasUnexpectedProfileFailure) {
        setLoadFailed(true);
        setLoading(false);
        return;
      }
      if (userResult.status === "fulfilled") {
        const selectedRole: Record<MarketplaceRole, SelectedRole> = { Blogger: "blogger", BrandFace: "brandFace", Business: "business" };
        const marketplaceRole = normalizeMarketplaceRole(userResult.value.selectedMarketplaceRole);
        if (marketplaceRole) setRole(selectedRole[marketplaceRole]);
      }
      if (bloggerResult.status === "fulfilled") setBlogger(bloggerResult.value);
      if (brandFaceResult.status === "fulfilled") setBrandFace(brandFaceResult.value);
      if (businessResult.status === "fulfilled") setBusiness(businessResult.value);
      if (applicationsResult.status === "fulfilled") setApplicationsCount(applicationsResult.value.length);
      if (dealsResult.status === "fulfilled") setDealsCount(dealsResult.value.length);
      setLoading(false);
    });
    return () => { cancelled = true; };
  }, []);

  useEffect(() => loadDashboard(), [loadDashboard]);
  useProfileDataRefresh(loadDashboard);

  const selectRole = async (nextRole: SelectedRole) => {
    if (switchingRole || nextRole === role) return;
    haptic.selection();
    const apiRole: Record<SelectedRole, MarketplaceRole> = { blogger: "Blogger", brandFace: "BrandFace", business: "Business" };
    try {
      setSwitchingRole(true);
      await selectMarketplaceRole(apiRole[nextRole]);
      setRole(nextRole);
      localStorage.setItem(selectedRoleKey, nextRole);
      onMarketplaceRoleSelected?.(apiRole[nextRole]);
      await refreshFavorites();
    } catch (error) {
      setToastTone("error");
      setToast(getApiErrorMessage(error, t("error.default")));
    } finally {
      setSwitchingRole(false);
    }
  };
  const username = telegramUser?.username ? `@${telegramUser.username}` : t("profile.usernameMissing");
  const activeProfile = role === "blogger" ? blogger : role === "brandFace" ? brandFace : business;
  const activeStoredImage = role === "blogger" ? blogger?.avatarUrl : role === "brandFace" ? brandFace?.avatarUrl : business?.logoUrl;
  const profileMediaTarget: ProfileMediaTarget = role === "blogger" ? "blogger" : role === "brandFace" ? "brand-face" : "business";
  const status = role === "brandFace" ? { label: t("profile.approved"), tone: "green" as const } : bloggerStatus(role === "blogger" ? blogger?.status : business?.moderationStatus, t);
  const profileHash = role === "blogger" ? "/blogger-form" : role === "brandFace" ? "/brand-face" : "/business";
  const completion = activeProfile ? role === "blogger" ? Math.round(([blogger?.bio, blogger?.phone, blogger?.email, blogger?.storiesPrice].filter(Boolean).length / 4) * 100) : role === "brandFace" ? Math.round(([brandFace?.experience, brandFace?.instagram, brandFace?.portfolioUrl, brandFace?.description].filter(Boolean).length / 4) * 100) : Math.round(([business?.description, business?.phone, business?.email, business?.logoUrl].filter(Boolean).length / 4) * 100) : 0;

  const updateProfileImage = (target: ProfileMediaTarget, imageUrl: string | null) => {
    if (target === "blogger") setBlogger((current) => current ? { ...current, avatarUrl: imageUrl } : current);
    else if (target === "brand-face") setBrandFace((current) => current ? { ...current, avatarUrl: imageUrl } : current);
    else setBusiness((current) => current ? { ...current, logoUrl: imageUrl } : current);
  };

  const changeAccountImage = async (image: PendingProfileImage) => {
    if (!activeProfile || accountImageSaving) return;
    setAccountImagePending(image);
    setAccountImageSaving(true);
    try {
      if (image instanceof File) {
        const media = await uploadProfileImage(profileMediaTarget, image);
        updateProfileImage(profileMediaTarget, media.url);
        notifyProfileDataChanged();
        haptic.success();
      } else if (image === null && activeStoredImage) {
        await deleteProfileImage(profileMediaTarget);
        updateProfileImage(profileMediaTarget, null);
        notifyProfileDataChanged();
        haptic.success();
      }
    } catch (error) {
      haptic.error();
      setToastTone("error");
      setToast(getApiErrorMessage(error, t("error.profile_media_unavailable")));
    } finally {
      setAccountImagePending(undefined);
      setAccountImageSaving(false);
    }
  };

  const clearLocalAccountState = () => {
    [localStorage, sessionStorage].forEach((storage) => {
      for (let index = storage.length - 1; index >= 0; index -= 1) {
        const key = storage.key(index);
        if (key?.startsWith("bloggerbazar.")) storage.removeItem(key);
      }
    });
    setLanguage("ru");
  };

  const resetLocalSession = () => {
    clearLocalAccountState();
    setLogoutOpen(false);
    setDeleteOpen(false);
    onSessionReset?.();
  };

  const logout = () => {
    resetLocalSession();
  };

  const requestAccountDeletion = async () => {
    if (deleting) return;
    setDeleting(true);
    try {
      await deleteCurrentAccount();
      haptic.success();
      resetLocalSession();
    } catch (error) {
      haptic.error();
      setToastTone("error");
      setToast(getApiErrorMessage(error, t("profile.deleteFailed")));
    } finally {
      setDeleting(false);
    }
  };

  return (
    <div className="screen screen--with-nav space-y-5 px-4 pt-5">
      <header className="flex items-center justify-between"><div><p className="text-sm font-semibold text-brand-muted">{t("profile.eyebrow")}</p><h1 className="mt-1 text-3xl font-extrabold tracking-tight">{t("profile.title")}</h1></div><LanguageSwitcher /></header>
      <Card className="flex items-center gap-4"><ProfileMediaPicker canRemove={Boolean(activeStoredImage)} compact currentUrl={activeStoredImage} disabled={!activeProfile || accountImageSaving} fallbackUrl={telegramUser?.photo_url} name={activeProfile?.name || telegramUser?.first_name || t("profile.telegramUser")} onChange={(image) => void changeAccountImage(image)} pending={accountImagePending} /><div className="min-w-0"><div className="truncate text-lg font-extrabold">{telegramUser?.first_name || t("profile.telegramUser")}</div><p className="mt-1 truncate text-sm text-brand-muted">{username}</p><Badge tone="blue">{t("profile.telegramAccount")}</Badge></div></Card>
      {loading ? <><Skeleton className="h-28" /><Skeleton className="h-20" /></> : loadFailed ? <ErrorState onRetry={loadDashboard} subtitle={t("common.connectionRetry")} title={t("common.openFailed")} /> : <>
        <section><div className="mb-3 flex items-center justify-between"><h2 className="text-lg font-extrabold">{t("profile.role")}</h2></div><div className="grid grid-cols-3 gap-3">
          <button aria-busy={switchingRole} className={`rounded-3xl border p-4 text-left transition ${role === "blogger" ? "border-brand-blue bg-blue-50 ring-2 ring-blue-100" : "border-brand-line bg-white"}`} disabled={switchingRole} onClick={() => void selectRole("blogger")} type="button"><Icon className="mb-3 text-brand-blue" name="user" /><div className="font-extrabold">{t("profile.blogger")}</div><div className="mt-1 text-xs text-brand-muted">{blogger ? t("profile.created") : t("profile.create")}</div></button>
          <button aria-busy={switchingRole} className={`rounded-3xl border p-4 text-left transition ${role === "brandFace" ? "border-brand-blue bg-blue-50 ring-2 ring-blue-100" : "border-brand-line bg-white"}`} disabled={switchingRole} onClick={() => void selectRole("brandFace")} type="button"><Icon className="mb-3 text-brand-blue" name="star" /><div className="font-extrabold">{t("onboarding.brandFace")}</div><div className="mt-1 text-xs text-brand-muted">{brandFace ? t("profile.created") : t("profile.create")}</div></button>
          <button aria-busy={switchingRole} className={`rounded-3xl border p-4 text-left transition ${role === "business" ? "border-brand-blue bg-blue-50 ring-2 ring-blue-100" : "border-brand-line bg-white"}`} disabled={switchingRole} onClick={() => void selectRole("business")} type="button"><Icon className="mb-3 text-brand-blue" name="building" /><div className="font-extrabold">{t("profile.business")}</div><div className="mt-1 text-xs text-brand-muted">{business ? t("profile.created") : t("profile.create")}</div></button>
        </div></section>
        {activeProfile ? <><Card><div className="flex items-start justify-between gap-3"><div><p className="text-sm font-semibold text-brand-muted">{role === "blogger" ? t("profile.bloggerProfile") : role === "brandFace" ? t("profile.brandFaceProfile") : t("profile.businessProfile")}</p><h2 className="mt-1 text-xl font-extrabold">{activeProfile.name}</h2><p className="mt-2 text-sm text-brand-muted">{role === "blogger" ? (blogger?.categories.join(" · ") || t("profile.categoryMissing")) : role === "brandFace" ? (brandFace?.categories.join(" · ") || t("profile.categoryMissing")) : (business?.city || t("profile.cityMissing"))}</p></div><Badge tone={status.tone}>{status.label}</Badge></div><div className="mt-4 h-2 overflow-hidden rounded-full bg-slate-100"><div className="h-full rounded-full bg-brand-gradient" style={{ width: `${completion}%` }} /></div><p className="mt-2 text-xs text-brand-muted">{t("profile.completion", { percent: completion })}</p><Button className="mt-4 w-full" onClick={() => { window.location.hash = profileHash; }} type="button">{t("profile.edit")}</Button></Card><Card className="border-blue-100 bg-gradient-to-br from-blue-50 via-white to-cyan-50"><p className="text-sm font-semibold text-brand-muted">{t("profile.status")}</p><p className="mt-1 text-lg font-extrabold">{status.label}</p><p className="mt-2 text-sm leading-5 text-brand-muted">{t("profile.publishedDescription")}</p></Card><div className="grid grid-cols-2 gap-3"><StatsCard icon="briefcase" label={t("common.applications")} value={String(applicationsCount)} /><StatsCard icon="check" label={t("common.deals", { count: dealsCount })} value={String(dealsCount)} /></div><a className="block" href="#/requests"><Card className="flex items-center justify-between"><span><strong>{t("profile.requestsAndDeals")}</strong><span className="mt-1 block text-sm text-brand-muted">{t("profile.requestsAndDealsSubtitle")}</span></span><Icon className="text-brand-blue" name="back" /></Card></a></> : <><EmptyState icon={role === "blogger" ? "user" : role === "brandFace" ? "star" : "building"} subtitle={t("profile.missingSubtitle")} title={role === "blogger" ? t("profile.missingBlogger") : role === "brandFace" ? t("profile.missingBrandFace") : t("profile.missingBusiness")} /><Button className="w-full" onClick={() => { window.location.hash = profileHash; }} type="button">{t("profile.create")}</Button></>}
      </>}
      {!loading && role !== "blogger" && <a className="block" href="#/favorites"><Card className="flex items-center justify-between"><span><strong>{t("favorites.profileTitle")}</strong><span className="mt-1 block text-sm text-brand-muted">{t("favorites.profileSubtitle")}</span></span><Icon className="text-brand-blue" name="back" /></Card></a>}
      <Card><div className="flex items-center justify-between"><div><div className="font-extrabold">BloggerBazar</div><div className="mt-1 text-sm text-brand-muted">{t("common.version", { version: "1.0.0" })}</div></div><a className="text-sm font-bold text-brand-blue" href="#/">{t("profile.backHome")}</a></div></Card>
      <div className="grid gap-3"><Button className="w-full" onClick={() => { haptic.warning(); setLogoutOpen(true); }} type="button" variant="danger">{t("profile.logout")}</Button><Button className="w-full text-brand-danger" onClick={() => { haptic.warning(); setDeleteOpen(true); }} type="button" variant="secondary">{t("profile.deleteAccount")}</Button></div>
      <Modal onClose={() => setLogoutOpen(false)} open={logoutOpen} title={t("profile.logoutTitle")}><p className="text-sm leading-6 text-brand-muted">{t("profile.logoutDescription")}</p><div className="mt-5 grid grid-cols-2 gap-3"><Button onClick={() => setLogoutOpen(false)} type="button" variant="secondary">{t("common.cancel")}</Button><Button onClick={logout} type="button" variant="danger">{t("profile.logout")}</Button></div></Modal>
      <Modal onClose={() => { if (!deleting) setDeleteOpen(false); }} open={deleteOpen} title={t("profile.deleteAccountTitle")}><p className="text-sm leading-6 text-brand-muted">{t("profile.deleteAccountDescription")}</p><div className="mt-5 grid grid-cols-2 gap-3"><Button disabled={deleting} onClick={() => setDeleteOpen(false)} type="button" variant="secondary">{t("common.cancel")}</Button><Button aria-busy={deleting} disabled={deleting} onClick={() => void requestAccountDeletion()} type="button" variant="danger">{deleting ? t("profile.deleting") : t("profile.deleteAccount")}</Button></div></Modal>
      <Toast message={toast} tone={toastTone} />
      <BottomNav />
    </div>
  );
}
