import { useCallback, useEffect, useRef, useState, type ReactNode } from "react";
import { getMarketplaceHome, type MarketplaceRole } from "../api/marketplace";
import { BloggerCard } from "../components/BloggerCard";
import { CampaignCard } from "../components/CampaignCard";
import { LanguageSwitcher } from "../components/LanguageSwitcher";
import { Avatar, BottomNav, Icon, Skeleton } from "../components/ui";
import { useProfileDataRefresh } from "../hooks/useProfileDataRefresh";
import { useScrollRestoration } from "../hooks/useScrollRestoration";
import { categoryLabel, cityLabel, useI18n } from "../i18n";
import { formatCurrency, formatNumber } from "../lib/currency";

type HomeData = Awaited<ReturnType<typeof getMarketplaceHome>>;
type HomeRole = MarketplaceRole;

type HeroContent = {
  title: string;
  description: string;
  primary: { label: string; href: string };
  secondary: Array<{ label: string; href: string }>;
};

function HomeSection({ title, actionHref, children }: { title: string; actionHref?: string; children: ReactNode }) {
  const { t } = useI18n();
  return <section aria-label={title} className="home-section" role="region">
    <div className="home-section__heading">
      <h2>{title}</h2>
      {actionHref && <a aria-label={t("home.viewAllSection", { section: title })} className="home-section__action" href={actionHref}>{t("home.viewAll")}</a>}
    </div>
    <div className="home-rail no-scrollbar">{children}</div>
  </section>;
}

function BrandFaceHomeCard({ profile }: { profile: HomeData["newBrandFaces"][number] }) {
  const { language, t } = useI18n();
  return <a aria-label={t("home.openBrandFace", { name: profile.name })} className="home-brand-face-card card-enter" href={`#/brand-face-detail/${profile.id}`}>
    <div className="flex min-w-0 items-start gap-3">
      <Avatar name={profile.name} size="sm" src={profile.avatarUrl} variant="home" />
      <div className="min-w-0 flex-1">
        <strong className="block truncate text-[15px] font-extrabold tracking-tight">{profile.name}</strong>
        <p className="mt-1 truncate text-xs text-[color:var(--bb-text-secondary)]">{cityLabel(profile.city, language)}</p>
      </div>
    </div>
    <div className="mt-3 flex flex-wrap gap-1.5">{profile.categories.slice(0, 2).map((category) => <span className="home-card__chip" key={category}>{categoryLabel(category, language)}</span>)}</div>
    <div className="home-brand-face-card__footer"><span>{t("common.price")}</span><strong>{profile.collaborationPrice == null ? t("card.onRequest") : formatCurrency(profile.collaborationPrice)}</strong></div>
  </a>;
}

function HomeHero({ role }: { role: HomeRole }) {
  const { t } = useI18n();
  const content: Record<HomeRole, HeroContent> = {
    Business: {
      title: t("home.businessHeroTitle"),
      description: t("home.businessHeroDescription"),
      primary: { label: t("home.findCreator"), href: "#/search" },
      secondary: [{ label: t("nav.campaigns"), href: "#/campaigns" }, { label: t("nav.requests"), href: "#/requests" }]
    },
    Blogger: {
      title: t("home.bloggerHeroTitle"),
      description: t("home.bloggerHeroDescription"),
      primary: { label: t("home.viewCampaigns"), href: "#/campaigns" },
      secondary: [{ label: t("home.myApplications"), href: "#/requests" }]
    },
    BrandFace: {
      title: t("home.brandFaceHeroTitle"),
      description: t("home.brandFaceHeroDescription"),
      primary: { label: t("home.openProfile"), href: "#/profile" },
      secondary: [{ label: t("home.viewCampaigns"), href: "#/campaigns" }]
    }
  };
  const hero = content[role];
  return <section aria-labelledby="home-hero-title" className="home-hero">
    <p className="home-hero__eyebrow">{t("common.appName")}</p>
    <h2 id="home-hero-title">{hero.title}</h2>
    <p className="home-hero__description">{hero.description}</p>
    <div className="home-hero__actions">
      <a aria-label={hero.primary.label} className="home-primary-action" href={hero.primary.href}>{hero.primary.label}</a>
      <div className="home-secondary-actions">{hero.secondary.map((action) => <a className="home-secondary-action" href={action.href} key={action.href}>{action.label}</a>)}</div>
    </div>
  </section>;
}

function HomeEmptyAction({ href, title, description }: { href: string; title: string; description: string }) {
  const { t } = useI18n();
  return <section aria-live="polite" className="home-inline-empty">
    <div><h2>{title}</h2><p>{description}</p></div>
    <a className="home-secondary-action" href={href}>{t("common.open")}</a>
  </section>;
}

function HomeError({ offline, onRetry }: { offline: boolean; onRetry: () => void }) {
  const { t } = useI18n();
  return <section aria-live="polite" className="home-error">
    <span aria-hidden="true" className="home-error__icon"><Icon name={offline ? "link" : "refresh"} /></span>
    <div><h2>{t(offline ? "home.offlineTitle" : "home.errorTitle")}</h2><p>{t(offline ? "home.offlineDescription" : "home.errorDescription")}</p></div>
    <button className="home-primary-action" onClick={onRetry} type="button"><Icon className="h-4 w-4" name="refresh" />{t("common.retry")}</button>
  </section>;
}

function HomeStatistics({ statistics }: { statistics: HomeData["statistics"] }) {
  const { t } = useI18n();
  const metrics = [
    { key: "bloggers", label: t("home.approvedBloggers"), value: formatNumber(statistics.approvedBloggers) },
    { key: "companies", label: t("home.companies"), value: formatNumber(statistics.companies) },
    { key: "campaigns", label: t("home.activeCampaigns"), value: formatNumber(statistics.activeCampaigns) },
    { key: "deals", label: t("home.completedDeals"), value: formatNumber(statistics.completedDeals) },
    ...(statistics.averageRating == null ? [] : [{ key: "rating", label: t("home.averageRating"), value: formatNumber(statistics.averageRating) }])
  ];
  return <section aria-label={t("home.statistics")} className="home-statistics">
    <div className="home-section__heading"><h2>{t("home.statistics")}</h2></div>
    <div className="home-statistics__grid">{metrics.map((metric) => <div className="home-statistics__item" key={metric.key}><strong>{metric.value}</strong><span>{metric.label}</span></div>)}</div>
  </section>;
}

export function Home({ role, initialData, initialError = false, initialLoading = false }: { role?: MarketplaceRole; initialData?: HomeData | null; initialError?: boolean; initialLoading?: boolean }) {
  const { language, t } = useI18n();
  const resolvedRole = role ?? "Business";
  const isPreview = initialData !== undefined || initialError || initialLoading;
  const [data, setData] = useState<HomeData | null>(initialData ?? null);
  const [failed, setFailed] = useState(initialError);
  const [offline, setOffline] = useState(false);
  const activeRequest = useRef<AbortController>();
  const requestVersion = useRef(0);
  useScrollRestoration("home");

  const load = useCallback(() => {
    if (isPreview) return;
    activeRequest.current?.abort();
    const controller = new AbortController();
    activeRequest.current = controller;
    const version = ++requestVersion.current;
    setFailed(false);
    setOffline(false);
    void getMarketplaceHome(controller.signal)
      .then((response) => {
        if (controller.signal.aborted || version !== requestVersion.current) return;
        setData(response);
      })
      .catch(() => {
        if (controller.signal.aborted || version !== requestVersion.current) return;
        setOffline(typeof navigator !== "undefined" && navigator.onLine === false);
        setFailed(true);
      });
  }, [isPreview]);

  useEffect(() => {
    if (isPreview) return;
    load();
    return () => activeRequest.current?.abort();
  }, [isPreview, load]);
  useProfileDataRefresh(load);

  const hasAnyBlogger = Boolean(data?.promotedBloggers.length || data?.topRatedBloggers.length || data?.newBloggers.length);
  const hasCampaigns = Boolean(data?.promotedCampaigns.length);

  return <div className="home screen screen--with-nav">
    <header className="home-header"><div><h1>{t("common.appName")}</h1><p>{t("home.marketplaceSubtitle")}</p></div><LanguageSwitcher /></header>
    <HomeHero role={resolvedRole} />
    {failed ? <HomeError offline={offline} onRetry={load} /> : !data ? <HomeSkeleton role={resolvedRole} /> : <div className="home-content">
      {resolvedRole === "Business" && <>
        <HomeCategories categories={data.categories} language={language} />
        {data.promotedBloggers.length > 0 && <HomeSection actionHref="#/search" title={t("home.promotedBloggers")}>{data.promotedBloggers.map((blogger) => <div className="home-rail__blogger" key={blogger.id}><BloggerCard blogger={blogger} variant="home" /></div>)}</HomeSection>}
        {data.topRatedBloggers.length > 0 && <HomeSection actionHref="#/search" title={t("home.topRated")}>{data.topRatedBloggers.map((blogger) => <div className="home-rail__blogger" key={blogger.id}><BloggerCard blogger={blogger} variant="home" /></div>)}</HomeSection>}
        {data.newBrandFaces.length > 0 && <HomeSection title={t("home.newBrandFaces")}>{data.newBrandFaces.map((profile) => <div className="home-rail__brand-face" key={profile.id}><BrandFaceHomeCard profile={profile} /></div>)}</HomeSection>}
        {data.newBloggers.length > 0 && <HomeSection actionHref="#/search" title={t("home.newBloggers")}>{data.newBloggers.map((blogger) => <div className="home-rail__blogger" key={blogger.id}><BloggerCard blogger={blogger} variant="home" /></div>)}</HomeSection>}
        {!hasAnyBlogger && <HomeEmptyAction description={t("home.businessNoCreatorsDescription")} href="#/search" title={t("home.businessNoCreatorsTitle")} />}
      </>}
      {resolvedRole === "Blogger" && <>
        {data.promotedCampaigns.length > 0 && <HomeSection actionHref="#/campaigns" title={t("home.promotedCampaigns")}>{data.promotedCampaigns.map((campaign) => <div className="home-rail__campaign" key={campaign.id}><CampaignCard campaign={campaign} variant="home" /></div>)}</HomeSection>}
        {!hasCampaigns && <HomeEmptyAction description={t("home.bloggerNoCampaignsDescription")} href="#/campaigns" title={t("home.bloggerNoCampaignsTitle")} />}
        {data.topRatedBloggers.length > 0 && <HomeSection actionHref="#/search" title={t("home.topRated")}>{data.topRatedBloggers.map((blogger) => <div className="home-rail__blogger" key={blogger.id}><BloggerCard blogger={blogger} variant="home" /></div>)}</HomeSection>}
        {data.newBloggers.length > 0 && <HomeSection actionHref="#/search" title={t("home.newBloggers")}>{data.newBloggers.map((blogger) => <div className="home-rail__blogger" key={blogger.id}><BloggerCard blogger={blogger} variant="home" /></div>)}</HomeSection>}
        <HomeCategories categories={data.categories} language={language} />
      </>}
      {resolvedRole === "BrandFace" && <>
        {data.newBrandFaces.length > 0 && <HomeSection title={t("home.newBrandFaces")}>{data.newBrandFaces.map((profile) => <div className="home-rail__brand-face" key={profile.id}><BrandFaceHomeCard profile={profile} /></div>)}</HomeSection>}
        {data.promotedCampaigns.length > 0 && <HomeSection actionHref="#/campaigns" title={t("home.promotedCampaigns")}>{data.promotedCampaigns.map((campaign) => <div className="home-rail__campaign" key={campaign.id}><CampaignCard campaign={campaign} variant="home" /></div>)}</HomeSection>}
        {data.topRatedBloggers.length > 0 && <HomeSection actionHref="#/search" title={t("home.topRated")}>{data.topRatedBloggers.map((blogger) => <div className="home-rail__blogger" key={blogger.id}><BloggerCard blogger={blogger} variant="home" /></div>)}</HomeSection>}
        {data.newBloggers.length > 0 && <HomeSection actionHref="#/search" title={t("home.newBloggers")}>{data.newBloggers.map((blogger) => <div className="home-rail__blogger" key={blogger.id}><BloggerCard blogger={blogger} variant="home" /></div>)}</HomeSection>}
      </>}
      <HomeStatistics statistics={data.statistics} />
    </div>}
    <BottomNav />
  </div>;
}

function HomeCategories({ categories, language }: { categories: string[]; language: "ru" | "uz" }) {
  const { t } = useI18n();
  if (categories.length === 0) return null;
  return <section aria-label={t("home.categories")} className="home-categories">
    <div className="home-section__heading"><h2>{t("home.categories")}</h2></div>
    <div className="home-categories__list">{categories.map((category) => <a aria-label={t("home.openCategory", { category: categoryLabel(category, language) })} className="home-category-chip" href={`#/search?category=${encodeURIComponent(category)}`} key={category}>{categoryLabel(category, language)}</a>)}</div>
  </section>;
}

function HomeSkeleton({ role }: { role: HomeRole }) {
  const { t } = useI18n();
  const showCampaignRail = role !== "Business";
  return <div aria-busy="true" aria-label={t("home.loading")} className="home-skeleton">
    <span className="sr-only">{t("home.loading")}</span>
    <section><Skeleton className="h-6 w-40" /><div className="home-skeleton__rail">{[0, 1].map((item) => <Skeleton className="h-48 w-[17.75rem] shrink-0" key={item} />)}</div></section>
    {showCampaignRail && <section><Skeleton className="h-6 w-44" /><div className="home-skeleton__rail">{[0, 1].map((item) => <Skeleton className="h-40 w-[17.75rem] shrink-0" key={item} />)}</div></section>}
    <section><Skeleton className="h-6 w-48" /><div className="grid grid-cols-2 gap-2"><Skeleton className="h-24" /><Skeleton className="h-24" /></div></section>
  </div>;
}
