import { useCallback, useEffect, useRef, useState, type FormEvent, type ReactNode } from "react";
import { getMarketplaceHome, getMyCampaigns, getMyDeals, getMyOffers, type MarketplaceRole } from "../api/marketplace";
import { BloggerCard } from "../components/BloggerCard";
import { BrandFaceCard } from "../components/BrandFaceCard";
import { CampaignCard } from "../components/CampaignCard";
import { BottomNav, Icon, Skeleton } from "../components/ui";
import { useProfileDataRefresh } from "../hooks/useProfileDataRefresh";
import { useScrollRestoration } from "../hooks/useScrollRestoration";
import { categoryLabel, useI18n } from "../i18n";
import { PageHeader } from "../components/PageHeader";
import { useScreenRefresh } from "../hooks/useScreenRefresh";

type HomeData = Awaited<ReturnType<typeof getMarketplaceHome>>;
type HomeRole = MarketplaceRole;

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

const platformChips = [
  ["instagram", "search.platformInstagram"],
  ["telegram", "search.platformTelegram"],
  ["tiktok", "search.platformTiktok"],
  ["youtube", "search.platformYoutube"]
] as const;

// Search is the main action of Home: Business looks for creators, creators look for campaigns.
function HomeSearch({ role, categories }: { role: HomeRole; categories: string[] }) {
  const { language, t } = useI18n();
  const [value, setValue] = useState("");
  const findsCreators = role === "Business";
  const route = findsCreators ? "/search" : "/campaigns";
  const submit = (event: FormEvent) => {
    event.preventDefault();
    const query = value.trim();
    window.location.hash = query ? `${route}?q=${encodeURIComponent(query)}` : route;
  };
  return <section aria-labelledby="home-search-title" className="home-search">
    <h2 id="home-search-title">{t(findsCreators ? "home.searchCreatorsTitle" : "home.searchCampaignsTitle")}</h2>
    <form className="home-search__form" onSubmit={submit} role="search">
      <Icon className="home-search__icon" name="search" />
      <input aria-label={t(findsCreators ? "home.searchCreatorsTitle" : "home.searchCampaignsTitle")} enterKeyHint="search" onChange={(event) => setValue(event.target.value)} placeholder={t(findsCreators ? "home.searchCreatorsPlaceholder" : "home.searchCampaignsPlaceholder")} type="search" value={value} />
      <button className="home-search__submit" type="submit">{t("home.searchSubmit")}</button>
    </form>
    <div aria-label={t("home.quickFilters")} className="home-quick-chips no-scrollbar" role="group">
      {findsCreators && platformChips.map(([platform, labelKey]) => <a className="home-category-chip" href={`#/search?platform=${platform}`} key={platform}>{t(labelKey)}</a>)}
      {categories.slice(0, 6).map((category) => <a aria-label={t("home.openCategory", { category: categoryLabel(category, language) })} className="home-category-chip" href={`#${route}?category=${encodeURIComponent(category)}`} key={category}>{categoryLabel(category, language)}</a>)}
    </div>
  </section>;
}

type ActivityItem = { key: string; title: string; detail: string; href: string };

// "Your tasks": only real, actionable items from existing endpoints; hidden when there is nothing to do.
function useHomeActivity(role: HomeRole, enabled: boolean, reloadKey: number) {
  const { t } = useI18n();
  const [items, setItems] = useState<ActivityItem[] | null>(null);
  useEffect(() => {
    // A brand face has deals and reviews like a blogger (D46).
    if (!enabled) { setItems([]); return; }
    let cancelled = false;
    const dealsRequest = getMyDeals();
    const roleRequest = role === "Business" ? getMyCampaigns({ pageSize: 20 }) : getMyOffers();
    void Promise.allSettled([dealsRequest, roleRequest]).then(([dealsResult, roleResult]) => {
      if (cancelled) return;
      const next: ActivityItem[] = [];
      if (roleResult.status === "fulfilled") {
        if (role === "Business") {
          // Only applications still waiting for a decision are a task; an older API without the field falls back to all.
          const waiting = (campaign: { applicationsCount: number; pendingApplicationsCount?: number }) => campaign.pendingApplicationsCount ?? campaign.applicationsCount;
          const campaigns = (roleResult.value as Awaited<ReturnType<typeof getMyCampaigns>>).items
            .filter((campaign) => waiting(campaign) > 0)
            .sort((left, right) => waiting(right) - waiting(left))
            .slice(0, 2);
          campaigns.forEach((campaign) => next.push({ key: `campaign-${campaign.id}`, title: campaign.title, detail: t("home.activityPendingApplications", { count: waiting(campaign) }), href: `#/my-campaign-applications/${campaign.id}` }));
        } else {
          (roleResult.value as Awaited<ReturnType<typeof getMyOffers>>).filter((offer) => offer.canRespond).slice(0, 2)
            .forEach((offer) => next.push({ key: `offer-${offer.id}`, title: t("home.activityOfferFrom", { name: offer.counterpartyName }), detail: t("home.activityOfferDetail"), href: `#/offer/${offer.id}` }));
        }
      }
      if (dealsResult.status === "fulfilled") {
        const active = dealsResult.value.filter((deal) => deal.status === 0).length;
        const toReview = dealsResult.value.filter((deal) => deal.canReview).length;
        if (active > 0) next.push({ key: "deals-active", title: t("home.activityActiveDeals", { count: active }), detail: t("home.activityActiveDealsDetail"), href: "#/requests?tab=deals" });
        if (toReview > 0) next.push({ key: "deals-review", title: t("home.activityReviews", { count: toReview }), detail: t("home.activityReviewsDetail"), href: "#/requests?tab=deals" });
      }
      setItems(next);
    });
    return () => { cancelled = true; };
  }, [enabled, reloadKey, role, t]);
  return items;
}

function HomeActivity({ items }: { items: ActivityItem[] }) {
  const { t } = useI18n();
  return <section aria-label={t("home.activityTitle")} className="home-activity">
    <div className="home-section__heading"><h2>{t("home.activityTitle")}</h2></div>
    <div className="home-activity__list">{items.map((item) => <a className="home-activity__item" href={item.href} key={item.key}>
      <span className="min-w-0"><strong>{item.title}</strong><span>{item.detail}</span></span>
      <Icon className="home-activity__chevron" name="back" />
    </a>)}</div>
  </section>;
}

function HomeHowItWorks({ role }: { role: HomeRole }) {
  const { t } = useI18n();
  const prefix = role === "Business" ? "home.howBusiness" : "home.howCreator";
  return <section aria-label={t("home.howTitle")} className="home-steps">
    <div className="home-section__heading"><h2>{t("home.howTitle")}</h2></div>
    <ol>{[1, 2, 3].map((step) => <li key={step}><span aria-hidden="true">{step}</span><div><strong>{t(`${prefix}${step}Title`)}</strong><p>{t(`${prefix}${step}Text`)}</p></div></li>)}</ol>
  </section>;
}

export function Home({ role, initialData, initialError = false, initialLoading = false }: { role?: MarketplaceRole; initialData?: HomeData | null; initialError?: boolean; initialLoading?: boolean }) {
  const { t } = useI18n();
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
  const [activityReloadKey, setActivityReloadKey] = useState(0);
  useScreenRefresh(() => { setActivityReloadKey((key) => key + 1); load(); });

  const activity = useHomeActivity(resolvedRole, !isPreview, activityReloadKey);
  const hasAnyBlogger = Boolean(data?.promotedBloggers.length || data?.topRatedBloggers.length || data?.newBloggers.length);
  const hasCampaigns = Boolean(data?.promotedCampaigns.length);
  const bloggerRail = (title: string, bloggers: HomeData["topRatedBloggers"]) => bloggers.length > 0 && <HomeSection actionHref="#/search" title={title}>{bloggers.map((blogger) => <div className="home-rail__blogger" key={blogger.id}><BloggerCard blogger={blogger} variant="home" /></div>)}</HomeSection>;
  const brandFaceRail = data && data.newBrandFaces.length > 0 && <HomeSection actionHref="#/search?type=brand-face" title={t("home.newBrandFaces")}>{data.newBrandFaces.map((profile) => <div className="home-rail__brand-face" key={profile.id}><BrandFaceCard profile={profile} variant="home" /></div>)}</HomeSection>;
  const campaignRail = data && data.promotedCampaigns.length > 0 && <HomeSection actionHref="#/campaigns" title={t("home.promotedCampaigns")}>{data.promotedCampaigns.map((campaign) => <div className="home-rail__campaign" key={campaign.id}><CampaignCard campaign={campaign} variant="home" /></div>)}</HomeSection>;

  return <div className="home screen screen--with-nav">
    <PageHeader eyebrow={t("home.marketplaceSubtitle")} title={t("common.appName")} />
    <HomeSearch categories={data?.categories ?? []} role={resolvedRole} />
    {activity && activity.length > 0 && <HomeActivity items={activity} />}
    {failed && !data ? <HomeError offline={offline} onRetry={load} /> : !data ? <HomeSkeleton role={resolvedRole} /> : <div className="home-content">
      {failed && <p className="text-sm text-brand-muted" role="status">{t("common.connectionRetry")}</p>}
      {resolvedRole === "Business" && <>
        {bloggerRail(t("home.promotedBloggers"), data.promotedBloggers)}
        {bloggerRail(t("home.topRated"), data.topRatedBloggers)}
        {brandFaceRail}
        {bloggerRail(t("home.newBloggers"), data.newBloggers)}
        {!hasAnyBlogger && <HomeEmptyAction description={t("home.businessNoCreatorsDescription")} href="#/search" title={t("home.businessNoCreatorsTitle")} />}
      </>}
      {resolvedRole === "Blogger" && <>
        {campaignRail}
        {!hasCampaigns && <HomeEmptyAction description={t("home.bloggerNoCampaignsDescription")} href="#/campaigns" title={t("home.bloggerNoCampaignsTitle")} />}
        {bloggerRail(t("home.topRated"), data.topRatedBloggers)}
      </>}
      {resolvedRole === "BrandFace" && <>
        {campaignRail}
        {brandFaceRail}
        {bloggerRail(t("home.topRated"), data.topRatedBloggers)}
      </>}
      {activity && activity.length === 0 && resolvedRole !== "BrandFace" && <HomeHowItWorks role={resolvedRole} />}
    </div>}
    <BottomNav />
  </div>;
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
