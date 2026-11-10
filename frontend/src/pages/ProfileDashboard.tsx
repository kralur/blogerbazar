import { useCallback, useEffect, useState } from "react";
import { deleteProfileImage, setProfileVisibility, getCurrentPlatformUser, getMyBloggerProfile, getMyBrandFaceProfile, getMyBusinessProfile, getMyCampaignApplications, getMyDeals, normalizeMarketplaceRole, selectMarketplaceRole, uploadProfileImage, type MarketplaceRole, type MyBloggerProfile, type MyBrandFaceProfile, type MyBusinessProfile, type ProfileMediaTarget } from "../api/marketplace";
import { ApiError, getApiErrorMessage } from "../api/client";
import { Badge, BottomNav, Button, Card, EmptyState, ErrorState, Icon, Modal, Skeleton, Toast } from "../components/ui";
import { categoryLabel, cityLabel, useI18n } from "../i18n";
import { useTelegramHandle } from "../components/TelegramHandleField";
import { profileCompleteness } from "../lib/profileCompleteness";
import { useTelegram } from "../telegram/TelegramProvider";
import { useFavorites } from "../features/favorites/FavoritesProvider";
import { useScrollRestoration } from "../hooks/useScrollRestoration";
import { ProfileMediaPicker, type PendingProfileImage } from "../components/ProfileMediaPicker";
import { notifyProfileDataChanged, useProfileDataRefresh } from "../hooks/useProfileDataRefresh";
import { ListHero } from "../components/ListHero";
import { PageHeader } from "../components/PageHeader";
import { useScreenRefresh } from "../hooks/useScreenRefresh";
import { isBrowserSession } from "../lib/browserSession";
import { useIsDesktop } from "../lib/desktopLayout";
import { DetailLayout } from "../components/DetailLayout";

type SelectedRole = "blogger" | "brandFace" | "business";
const selectedRoleKey = "bloggerbazar.selectedRole";

function bloggerStatus(status: number | undefined, t: (key: string) => string) {
  if (status === 1) return { label: t("profile.approved"), tone: "green" as const };
  if (status === 2) return { label: t("profile.rejected"), tone: "gray" as const };
  if (status === 4) return { label: t("profile.needsChanges"), tone: "orange" as const };
  return { label: t("profile.pending"), tone: "gold" as const };
}

export function ProfileDashboard({ onMarketplaceRoleSelected }: { onMarketplaceRoleSelected?: (role: MarketplaceRole) => void }) {
  const { haptic, user: telegramUser } = useTelegram();
  const telegramHandleValue = useTelegramHandle();
  useScrollRestoration("profile");
  const [blogger, setBlogger] = useState<MyBloggerProfile | null>(null);
  const [brandFace, setBrandFace] = useState<MyBrandFaceProfile | null>(null);
  const [business, setBusiness] = useState<MyBusinessProfile | null>(null);
  const [loading, setLoading] = useState(true);
  const [loadFailed, setLoadFailed] = useState(false);
  const [toast, setToast] = useState("");
  const [toastTone, setToastTone] = useState<"success" | "error">("error");
  const [accountImagePending, setAccountImagePending] = useState<PendingProfileImage>();
  const [accountImageSaving, setAccountImageSaving] = useState(false);
  const [switchingRole, setSwitchingRole] = useState(false);
  const [hideConfirmOpen, setHideConfirmOpen] = useState(false);
  const [visibilitySaving, setVisibilitySaving] = useState(false);
  const [applicationsCount, setApplicationsCount] = useState<number | null>(null);
  const [dealsCount, setDealsCount] = useState<number | null>(null);
  const [role, setRole] = useState<SelectedRole>(() => {
    const savedRole = localStorage.getItem(selectedRoleKey);
    return savedRole === "business" || savedRole === "brandFace" ? savedRole : "blogger";
  });
  const { language, t: translate } = useI18n();
  const t = (key: string, values?: Record<string, string | number>) => translate(
    key === "profile.requestsAndDeals" ? "requests.title" : key === "profile.requestsAndDealsSubtitle" ? "requests.emptyApplicationsSubtitle" : key,
    values
  );
  const { refreshFavorites } = useFavorites();
  // D64 stage B: a browser session works for the company only; roles are switched in Telegram.
  const inBrowser = isBrowserSession();
  const desktop = useIsDesktop();

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
  useScreenRefresh(loadDashboard);

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
  const username = telegramHandleValue ?? t("profile.usernameMissing");
  const activeProfile = role === "blogger" ? blogger : role === "brandFace" ? brandFace : business;
  // A brand face without what a business filters and looks at is hard to find (QA Q20).
  const brandFaceIncomplete = role === "brandFace" && Boolean(brandFace) && (!brandFace?.instagram || !brandFace?.gender || !brandFace?.age || !(brandFace?.formats?.length) || (!brandFace?.avatarUrl && !(brandFace?.photoUrls?.length)));
  const activeStoredImage = role === "blogger" ? blogger?.avatarUrl : role === "brandFace" ? brandFace?.avatarUrl : business?.logoUrl;
  const profileMediaTarget: ProfileMediaTarget = role === "blogger" ? "blogger" : role === "brandFace" ? "brand-face" : "business";
  const moderation = role === "brandFace" ? { label: t("profile.approved"), tone: "green" as const } : bloggerStatus(role === "blogger" ? blogger?.status : business?.moderationStatus, t);
  // D50: a paused profile shows it instead of "Active"; moderation still decides whether a public page exists.
  const activeHidden = Boolean(activeProfile?.isHidden);
  // D64: a manager or a viewer in the company's team works with it but does not change its profile.
  const profileReadOnly = role === "business" && Boolean(business?.teamRole) && business?.teamRole !== "owner" && business?.teamRole !== "admin";
  const status = activeHidden && moderation.tone === "green" ? { label: t("profile.hidden"), tone: "gray" as const } : moderation;
  const profileHash = role === "blogger" ? "/blogger-form" : role === "brandFace" ? "/brand-face" : "/business";
  // Only an approved profile is public, so only then is there a page to preview.
  const publicHref = moderation.tone !== "green" || !activeProfile ? null
    : role === "blogger" ? `#/blogger/${activeProfile.id}` : role === "brandFace" ? `#/brand-face-detail/${activeProfile.id}` : `#/company/${activeProfile.id}`;
  // The same measure as Home's progress card, so both say the same percent.
  const completion = activeProfile ? profileCompleteness(role === "blogger" ? "Blogger" : role === "brandFace" ? "BrandFace" : "Business", activeProfile).percent : 0;

  const updateHidden = (target: ProfileMediaTarget, isHidden: boolean) => {
    if (target === "blogger") setBlogger((current) => current ? { ...current, isHidden } : current);
    else if (target === "brand-face") setBrandFace((current) => current ? { ...current, isHidden } : current);
    else setBusiness((current) => current ? { ...current, isHidden } : current);
  };

  const changeVisibility = async (hidden: boolean) => {
    if (!activeProfile || visibilitySaving) return;
    setVisibilitySaving(true);
    try {
      await setProfileVisibility(profileMediaTarget, hidden);
      updateHidden(profileMediaTarget, hidden);
      setHideConfirmOpen(false);
      notifyProfileDataChanged();
      haptic.success();
      setToastTone("success");
      setToast(t(hidden ? "profile.hiddenDone" : "profile.shownDone"));
    } catch (error) {
      haptic.error();
      setToastTone("error");
      setToast(getApiErrorMessage(error, t("profile.visibilityFailed")));
    } finally {
      setVisibilitySaving(false);
    }
  };

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

  const settingsLink = <nav aria-label={t("profile.settings")} className="settings-list"><SettingsLink detail={t("profile.settingsDetail")} href="#/settings" title={t("profile.settings")} /></nav>;
  const shortcuts = <nav aria-label={t("profile.shortcuts")} className="settings-list"><SettingsLink detail={applicationsCount == null || dealsCount == null ? t("requests.eyebrow") : t("profile.requestsSummary", { applications: applicationsCount, deals: dealsCount })} href="#/requests" title={t("profile.requestsAndDeals")} />{role === "business" && <SettingsLink detail={t("profile.myCampaignsDetail")} href="#/my-campaigns" title={t("myCampaigns.title")} />}{role === "business" && business && <SettingsLink detail={business.teamMode ? t("team.profileWithTeam") : t("team.profileAlone")} href="#/team" title={t("team.title")} />}{role !== "blogger" && <SettingsLink detail={t("favorites.profileSubtitle")} href="#/favorites" title={t("favorites.profileTitle")} />}</nav>;
  return (
    <div className="screen screen--with-nav space-y-5 px-4 pt-5">
      <ListHero className="profile-hero">
      <PageHeader eyebrow={t("profile.eyebrow")} title={t("profile.title")} />
      <Card className="profile-hero__account flex items-center gap-4"><ProfileMediaPicker canRemove={Boolean(activeStoredImage)} compact currentUrl={activeStoredImage} disabled={!activeProfile || accountImageSaving || profileReadOnly} fallbackUrl={activeProfile ? undefined : telegramUser?.photo_url} name={activeProfile?.name || telegramUser?.first_name || t("profile.telegramUser")} onChange={(image) => void changeAccountImage(image)} pending={accountImagePending} /><div className="min-w-0"><div className="truncate text-lg font-extrabold">{activeProfile?.name || telegramUser?.first_name || t("profile.telegramUser")}</div><p className="mt-1 truncate text-sm text-brand-muted">{username}</p><Badge tone="blue">{t("profile.telegramAccount")}</Badge></div></Card>
      {!loading && !loadFailed && !inBrowser && <>
        <section className="profile-hero__roles"><h2>{t("profile.role")}</h2><div className="grid grid-cols-3 gap-3">
          <button aria-busy={switchingRole} className={`rounded-3xl border p-4 text-left transition ${role === "blogger" ? "profile-role-card--selected" : "profile-role-card"}${blogger ? "" : " profile-role-card--missing"}`} disabled={switchingRole} onClick={() => void selectRole("blogger")} type="button"><Icon className="mb-3 text-brand-ink" name="user" /><div className="font-extrabold">{t("profile.blogger")}</div><div className="mt-1 text-xs text-brand-muted">{blogger ? t("profile.created") : t("profile.create")}</div></button>
          <button aria-busy={switchingRole} className={`rounded-3xl border p-4 text-left transition ${role === "brandFace" ? "profile-role-card--selected" : "profile-role-card"}${brandFace ? "" : " profile-role-card--missing"}`} disabled={switchingRole} onClick={() => void selectRole("brandFace")} type="button"><Icon className="mb-3 text-brand-ink" name="star" /><div className="font-extrabold">{t("onboarding.brandFace")}</div><div className="mt-1 text-xs text-brand-muted">{brandFace ? t("profile.created") : t("profile.create")}</div></button>
          <button aria-busy={switchingRole} className={`rounded-3xl border p-4 text-left transition ${role === "business" ? "profile-role-card--selected" : "profile-role-card"}${business ? "" : " profile-role-card--missing"}`} disabled={switchingRole} onClick={() => void selectRole("business")} type="button"><Icon className="mb-3 text-brand-ink" name="building" /><div className="font-extrabold">{t("profile.business")}</div><div className="mt-1 text-xs text-brand-muted">{business ? t("profile.created") : t("profile.create")}</div></button>
        </div></section>
      </>}
      </ListHero>
      {loading ? <><Skeleton className="h-28" /><Skeleton className="h-20" /></> : loadFailed ? <ErrorState onRetry={loadDashboard} subtitle={t("common.connectionRetry")} title={t("common.openFailed")} /> : <>
        {activeProfile ? <DetailLayout side={<>{shortcuts}{desktop && settingsLink}</>}><Card><div className="flex items-start justify-between gap-3"><div><p className="text-sm font-semibold text-brand-muted">{role === "blogger" ? t("profile.bloggerProfile") : role === "brandFace" ? t("profile.brandFaceProfile") : t("profile.businessProfile")}</p><p className="mt-2 text-sm text-brand-muted">{role === "blogger" ? (blogger?.categories.map((category) => categoryLabel(category, language)).join(" · ") || t("profile.categoryMissing")) : role === "brandFace" ? (brandFace?.categories.map((category) => categoryLabel(category, language)).join(" · ") || t("profile.categoryMissing")) : (business?.city ? cityLabel(business.city, language) : t("profile.cityMissing"))}</p></div><Badge tone={status.tone}>{status.label}</Badge></div><div className="mt-4 h-2 overflow-hidden rounded-full bg-brand-line"><div className="h-full rounded-full bg-brand-accent" style={{ width: `${completion}%` }} /></div><p className="mt-2 text-xs text-brand-muted">{t("profile.completion", { percent: completion })}</p>{activeHidden && desktop && <p className="mt-3 text-sm leading-5 text-brand-muted">{t("profile.hiddenDescription")}</p>}<div className="profile-card__actions">{!profileReadOnly && <Button className="mt-4 w-full" onClick={() => { window.location.hash = profileHash; }} type="button">{t("profile.edit")}</Button>}{publicHref && <a className="profile-public-link" href={publicHref}>{t("profile.viewPublic")}</a>}{activeHidden && !desktop && <p className="mt-3 text-sm leading-5 text-brand-muted">{t("profile.hiddenDescription")}</p>}{!profileReadOnly && <Button aria-busy={visibilitySaving} className="mt-3 w-full" disabled={visibilitySaving} onClick={() => activeHidden ? void changeVisibility(false) : setHideConfirmOpen(true)} type="button" variant="secondary">{t(activeHidden ? "profile.show" : "profile.hide")}</Button>}</div></Card>{!activeStoredImage && activeProfile && !profileReadOnly && <p className="brand-face-soon mt-4" role="note">{t(role === "business" ? "profile.logoMissingHint" : "profile.photoMissingHint")}</p>}{brandFaceIncomplete && <div className="brand-face-soon mt-4" role="note"><p>{t("brandFace.completeHint")}</p><a className="campaign-details__blocked-action" href="#/brand-face">{t("brandFace.completeAction")}</a></div>}</DetailLayout> : <><EmptyState icon={role === "blogger" ? "user" : role === "brandFace" ? "star" : "building"} subtitle={t("profile.missingSubtitle")} title={role === "blogger" ? t("profile.missingBlogger") : role === "brandFace" ? t("profile.missingBrandFace") : t("profile.missingBusiness")} /><Button className="w-full" onClick={() => { window.location.hash = profileHash; }} type="button">{t("profile.create")}</Button></>}
      </>}
      {/* Settings stay reachable even without an active profile: logout and account deletion live there. */}
      {!(desktop && activeProfile && !loading && !loadFailed) && settingsLink}
      <Modal onClose={() => !visibilitySaving && setHideConfirmOpen(false)} open={hideConfirmOpen} title={t("profile.hideTitle")}><p className="text-sm leading-6 text-brand-muted">{t("profile.hideDescription")}</p><div className="mt-5 modal-actions"><Button disabled={visibilitySaving} onClick={() => setHideConfirmOpen(false)} type="button" variant="secondary">{t("common.cancel")}</Button><Button aria-busy={visibilitySaving} disabled={visibilitySaving} onClick={() => void changeVisibility(true)} type="button">{t("profile.hideConfirm")}</Button></div></Modal><Toast message={toast} tone={toastTone} />
      <BottomNav />
    </div>
  );
}

function SettingsLink({ href, title, detail }: { href: string; title: string; detail: string }) {
  return <a className="settings-row" href={href}><span className="settings-row__text"><strong>{title}</strong><span>{detail}</span></span><Icon className="settings-row__chevron" name="back" /></a>;
}
