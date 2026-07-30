import { useEffect, useState } from "react";
import { getCurrentPlatformUser, getMyBloggerProfile, getMyBrandFaceProfile, getMyBusinessProfile, getMyCampaignApplications, getMyDeals, selectMarketplaceRole, type MarketplaceRole, type MyBloggerProfile, type MyBrandFaceProfile, type MyBusinessProfile } from "../api/marketplace";
import { Avatar, Badge, BottomNav, Button, Card, EmptyState, Icon, Modal, Skeleton, StatsCard } from "../components/ui";
import { useI18n } from "../i18n";
import { useTelegram } from "../telegram/TelegramProvider";

type SelectedRole = "blogger" | "brandFace" | "business";
const selectedRoleKey = "bloggerbazar.selectedRole";
const languageKey = "bloggerbazar.language";

function bloggerStatus(status: number | undefined, t: (key: string) => string) {
  if (status === 1) return { label: t("profile.approved"), tone: "green" as const };
  if (status === 2) return { label: t("profile.rejected"), tone: "gray" as const };
  if (status === 4) return { label: t("profile.needsChanges"), tone: "orange" as const };
  return { label: t("profile.pending"), tone: "gold" as const };
}

export function ProfileDashboard() {
  const { user: telegramUser } = useTelegram();
  const [blogger, setBlogger] = useState<MyBloggerProfile | null>(null);
  const [brandFace, setBrandFace] = useState<MyBrandFaceProfile | null>(null);
  const [business, setBusiness] = useState<MyBusinessProfile | null>(null);
  const [loading, setLoading] = useState(true);
  const [logoutOpen, setLogoutOpen] = useState(false);
  const [applicationsCount, setApplicationsCount] = useState(0);
  const [dealsCount, setDealsCount] = useState(0);
  const [role, setRole] = useState<SelectedRole>(() => {
    const savedRole = localStorage.getItem(selectedRoleKey);
    return savedRole === "business" || savedRole === "brandFace" ? savedRole : "blogger";
  });
  const { language, setLanguage, t } = useI18n();

  useEffect(() => {
    let cancelled = false;
    Promise.allSettled([getCurrentPlatformUser(), getMyBloggerProfile(), getMyBrandFaceProfile(), getMyBusinessProfile(), getMyCampaignApplications(), getMyDeals()]).then(([userResult, bloggerResult, brandFaceResult, businessResult, applicationsResult, dealsResult]) => {
      if (cancelled) return;
      if (userResult.status === "fulfilled") {
        const selectedRole: Record<MarketplaceRole, SelectedRole> = { Blogger: "blogger", BrandFace: "brandFace", Business: "business" };
        if (userResult.value.selectedMarketplaceRole) setRole(selectedRole[userResult.value.selectedMarketplaceRole]);
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

  const selectRole = (nextRole: SelectedRole) => {
    setRole(nextRole);
    localStorage.setItem(selectedRoleKey, nextRole);
    const apiRole: Record<SelectedRole, MarketplaceRole> = { blogger: "Blogger", brandFace: "BrandFace", business: "Business" };
    void selectMarketplaceRole(apiRole[nextRole]).catch(() => undefined);
  };
  const username = telegramUser?.username ? `@${telegramUser.username}` : t("profile.usernameMissing");
  const activeProfile = role === "blogger" ? blogger : role === "brandFace" ? brandFace : business;
  const status = role === "brandFace" ? { label: t("profile.approved"), tone: "green" as const } : bloggerStatus(role === "blogger" ? blogger?.status : business?.moderationStatus, t);
  const profileHash = role === "blogger" ? "/blogger-form" : role === "brandFace" ? "/brand-face" : "/business";
  const completion = activeProfile ? role === "blogger" ? Math.round(([blogger?.bio, blogger?.phone, blogger?.email, blogger?.storiesPrice].filter(Boolean).length / 4) * 100) : role === "brandFace" ? Math.round(([brandFace?.experience, brandFace?.instagram, brandFace?.portfolioUrl, brandFace?.description].filter(Boolean).length / 4) * 100) : Math.round(([business?.description, business?.phone, business?.email, business?.logoUrl].filter(Boolean).length / 4) * 100) : 0;

  const logout = () => {
    localStorage.removeItem(selectedRoleKey);
    localStorage.removeItem("bloggerbazar.preferences");
    localStorage.removeItem(languageKey);
    localStorage.removeItem("bloggerbazar.onboarding.welcomeViewed");
    localStorage.removeItem("bloggerbazar.onboarding.completed");
    setLanguage("ru");
    window.location.hash = "/";
    setLogoutOpen(false);
  };

  return (
    <div className="screen space-y-5 px-4 pt-5">
      <header className="flex items-center justify-between"><div><p className="text-sm font-semibold text-brand-muted">{t("profile.eyebrow")}</p><h1 className="mt-1 text-3xl font-extrabold tracking-tight">{t("profile.title")}</h1></div><button aria-label={t("language.aria")} className="grid h-11 w-11 place-items-center rounded-2xl bg-white text-xs font-extrabold text-brand-blue shadow-card" onClick={() => setLanguage(language === "ru" ? "uz" : "ru")} type="button">{language === "ru" ? "UZ" : "RU"}</button></header>
      <Card className="flex items-center gap-4"><Avatar name={telegramUser?.first_name || t("profile.telegramUser")} size="md" /><div className="min-w-0"><div className="truncate text-lg font-extrabold">{telegramUser?.first_name || t("profile.telegramUser")}</div><p className="mt-1 truncate text-sm text-brand-muted">{username}</p><Badge tone="blue">{t("profile.telegramAccount")}</Badge></div></Card>
      {loading ? <><Skeleton className="h-28" /><Skeleton className="h-20" /></> : <>
        <section><div className="mb-3 flex items-center justify-between"><h2 className="text-lg font-extrabold">{t("profile.role")}</h2><span className="text-sm text-brand-muted">{t("common.synced")}</span></div><div className="grid grid-cols-3 gap-3">
          <button className={`rounded-3xl border p-4 text-left transition ${role === "blogger" ? "border-brand-blue bg-blue-50 ring-2 ring-blue-100" : "border-brand-line bg-white"}`} onClick={() => selectRole("blogger")} type="button"><Icon className="mb-3 text-brand-blue" name="user" /><div className="font-extrabold">{t("profile.blogger")}</div><div className="mt-1 text-xs text-brand-muted">{blogger ? t("profile.created") : t("profile.create")}</div></button>
          <button className={`rounded-3xl border p-4 text-left transition ${role === "brandFace" ? "border-brand-blue bg-blue-50 ring-2 ring-blue-100" : "border-brand-line bg-white"}`} onClick={() => selectRole("brandFace")} type="button"><Icon className="mb-3 text-brand-blue" name="star" /><div className="font-extrabold">{t("onboarding.brandFace")}</div><div className="mt-1 text-xs text-brand-muted">{brandFace ? t("profile.created") : t("profile.create")}</div></button>
          <button className={`rounded-3xl border p-4 text-left transition ${role === "business" ? "border-brand-blue bg-blue-50 ring-2 ring-blue-100" : "border-brand-line bg-white"}`} onClick={() => selectRole("business")} type="button"><Icon className="mb-3 text-brand-blue" name="building" /><div className="font-extrabold">{t("profile.business")}</div><div className="mt-1 text-xs text-brand-muted">{business ? t("profile.created") : t("profile.create")}</div></button>
        </div></section>
        {activeProfile ? <><Card><div className="flex items-start justify-between gap-3"><div><p className="text-sm font-semibold text-brand-muted">{role === "blogger" ? t("profile.bloggerProfile") : role === "brandFace" ? t("profile.brandFaceProfile") : t("profile.businessProfile")}</p><h2 className="mt-1 text-xl font-extrabold">{activeProfile.name}</h2><p className="mt-2 text-sm text-brand-muted">{role === "blogger" ? (blogger?.categories.join(" · ") || t("profile.categoryMissing")) : role === "brandFace" ? (brandFace?.categories.join(" · ") || t("profile.categoryMissing")) : (business?.city || t("profile.cityMissing"))}</p></div><Badge tone={status.tone}>{status.label}</Badge></div><div className="mt-4 h-2 overflow-hidden rounded-full bg-slate-100"><div className="h-full rounded-full bg-brand-gradient" style={{ width: `${completion}%` }} /></div><p className="mt-2 text-xs text-brand-muted">{t("profile.completion", { percent: completion })}</p><Button className="mt-4 w-full" onClick={() => { window.location.hash = profileHash; }} type="button">{t("profile.edit")}</Button></Card><Card className="border-blue-100 bg-gradient-to-br from-blue-50 via-white to-cyan-50"><p className="text-sm font-semibold text-brand-muted">{t("profile.status")}</p><p className="mt-1 text-lg font-extrabold">{status.label}</p><p className="mt-2 text-sm leading-5 text-brand-muted">{t("profile.publishedDescription")}</p></Card><div className="grid grid-cols-2 gap-3"><StatsCard icon="briefcase" label={t("common.applications")} value={String(applicationsCount)} /><StatsCard icon="check" label={t("common.deals", { count: dealsCount })} value={String(dealsCount)} /></div><a className="block" href="#/requests"><Card className="flex items-center justify-between"><span><strong>{t("profile.requestsAndDeals")}</strong><span className="mt-1 block text-sm text-brand-muted">{t("profile.requestsAndDealsSubtitle")}</span></span><Icon className="text-brand-blue" name="back" /></Card></a></> : <><EmptyState icon={role === "blogger" ? "user" : role === "brandFace" ? "star" : "building"} subtitle={t("profile.missingSubtitle")} title={role === "blogger" ? t("profile.missingBlogger") : role === "brandFace" ? t("profile.missingBrandFace") : t("profile.missingBusiness")} /><Button className="w-full" onClick={() => { window.location.hash = profileHash; }} type="button">{t("profile.create")}</Button></>}
      </>}
      <Card><div className="flex items-center justify-between"><div><div className="font-extrabold">BloggerBazar</div><div className="mt-1 text-sm text-brand-muted">{t("common.version", { version: "1.0.0" })}</div></div><a className="text-sm font-bold text-brand-blue" href="#/">{t("profile.backHome")}</a></div></Card>
      <Button className="w-full" onClick={() => setLogoutOpen(true)} type="button" variant="danger">{t("profile.logout")}</Button>
      <Modal onClose={() => setLogoutOpen(false)} open={logoutOpen} title={t("profile.logoutTitle")}><p className="text-sm leading-6 text-brand-muted">{t("profile.logoutDescription")}</p><div className="mt-5 grid grid-cols-2 gap-3"><Button onClick={() => setLogoutOpen(false)} type="button" variant="secondary">{t("common.cancel")}</Button><Button onClick={logout} type="button" variant="danger">{t("profile.logout")}</Button></div></Modal>
      <BottomNav />
    </div>
  );
}
