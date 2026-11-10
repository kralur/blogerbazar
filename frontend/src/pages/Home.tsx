import { useCallback, useEffect, useRef, useState, type FormEvent, type ReactNode } from "react";
import { getMarketplaceHome, getMyBloggerProfile, getMyBrandFaceProfile, getMyBusinessProfile, getMyCampaigns, getMyDeals, getMyOffers, getRecommendations, type MarketplaceRole } from "../api/marketplace";
import { profileCompleteness, type ProfileCompleteness } from "../lib/profileCompleteness";
import { readHomeSnapshot, writeHomeSnapshot } from "../data/homeSnapshot";
import type { BloggerCardData } from "../components/BloggerCard";
import type { CampaignCardData } from "../components/CampaignCard";
import { AtlasStrip } from "../components/AtlasStrip";
import { SeasonArt } from "../components/SeasonArt";
import { SeasonNote } from "../components/SeasonNote";
import { Avatar, BottomNav, Icon, Skeleton } from "../components/ui";
import { useProfileDataRefresh } from "../hooks/useProfileDataRefresh";
import { useScrollRestoration } from "../hooks/useScrollRestoration";
import { categoryLabel, cityLabel, useI18n } from "../i18n";
import { formatCompactNumber, formatNumber, formatShortDate } from "../lib/currency";
import { useScreenRefresh } from "../hooks/useScreenRefresh";

type HomeData = Awaited<ReturnType<typeof getMarketplaceHome>>;
type Recommendations = Awaited<ReturnType<typeof getRecommendations>>;
type BrandFaceRow = HomeData["newBrandFaces"][number];

// D53: the "For you" rail; a missing or failed answer simply hides it.
function useRecommendations(enabled: boolean, reloadKey: number) {
  const [value, setValue] = useState<Recommendations | null>(null);
  useEffect(() => {
    if (!enabled) return;
    const controller = new AbortController();
    getRecommendations(controller.signal).then((result) => { if (!controller.signal.aborted) setValue(result); }).catch(() => undefined);
    return () => controller.abort();
  }, [enabled, reloadKey]);
  return value;
}
type HomeRole = MarketplaceRole;

// The selected role's own profile: its name and city for the top of the hero, its completeness for the progress
// bar. A failed or missing profile hides both.
type OwnProfile = { name: string; city?: string | null; progress: ProfileCompleteness };
function useOwnProfile(role: HomeRole, enabled: boolean, reloadKey: number) {
  const [value, setValue] = useState<OwnProfile | null>(null);
  useEffect(() => {
    if (!enabled) return;
    let cancelled = false;
    const request = role === "Business" ? getMyBusinessProfile() : role === "BrandFace" ? getMyBrandFaceProfile() : getMyBloggerProfile();
    request.then((profile) => {
      if (cancelled) return;
      const lastName = "lastName" in profile ? profile.lastName : null;
      setValue({ name: [profile.name, lastName].filter(Boolean).join(" "), city: profile.city, progress: profileCompleteness(role, profile) });
    }).catch(() => { if (!cancelled) setValue(null); });
    return () => { cancelled = true; };
  }, [enabled, reloadKey, role]);
  return value;
}

function HomeProgress({ progress }: { progress: ProfileCompleteness }) {
  const { t } = useI18n();
  const title = t("home.progress.title", { percent: progress.percent });
  const next = progress.next!;
  return <section aria-label={title} className="home-progress" role="region">
    <div className="home-progress__head"><h2>{title}</h2><a className="home-progress__action" href={next.href}>{t("home.progress.action")}</a></div>
    <div aria-label={title} aria-valuemax={100} aria-valuemin={0} aria-valuenow={progress.percent} className="home-progress__track" role="progressbar"><span className="home-progress__value" style={{ width: `${Math.max(progress.percent, 4)}%` }} /></div>
    <p>{t("home.progress.next", { step: t(`home.progress.${next.key}`) })}</p>
  </section>;
}

function HomeSection({ title, actionHref, children }: { title: string; actionHref?: string; children: ReactNode }) {
  const { t } = useI18n();
  return <section aria-label={title} className="home-section" role="region">
    <div className="home-section__heading">
      <h2>{title}</h2>
      {actionHref && <a aria-label={t("home.viewAllSection", { section: title })} className="home-section__action" href={actionHref}>{t("home.viewAll")}</a>}
    </div>
    <div className="home-list">{children}</div>
  </section>;
}

// One row of a Home list (D62): avatar, name, a line of facts, the price on the right when the API sent one.
function HomeRow({ href, avatarUrl, avatarName, name, meta, price, unit, neutral = false }: { href: string; avatarUrl?: string | null; avatarName: string; name: string; meta: string; price?: string | null; unit?: string; neutral?: boolean }) {
  return <a className="home-row" href={href}>
    <Avatar name={avatarName} priority size="sm" src={avatarUrl} variant={neutral ? "neutral" : "catalog"} />
    <span className="home-row__text"><strong>{name}</strong><span>{meta}</span></span>
    {price && <span className="home-row__price"><strong>{price}</strong>{unit && <span>{unit}</span>}</span>}
  </a>;
}

// Millions are shown short (13.2 mln), so a big budget leaves room for the title; smaller sums stay exact.
const rowAmount = (value: number) => value >= 1_000_000 ? formatCompactNumber(value) : formatNumber(value);

function BloggerRow({ blogger }: { blogger: BloggerCardData }) {
  const { language, t } = useI18n();
  const meta = [cityLabel(blogger.city, language), ...blogger.categories.slice(0, 2).map((category) => categoryLabel(category, language))].join(", ");
  const [price, unit] = blogger.reelsPrice ? [rowAmount(blogger.reelsPrice), t("home.row.perReels")]
    : blogger.storiesPrice ? [rowAmount(blogger.storiesPrice), t("home.row.perStories")]
      : blogger.priceFrom ? [t("home.row.from", { amount: rowAmount(blogger.priceFrom) }), t("currency.uzs")] : [null, undefined];
  return <HomeRow avatarName={blogger.name} avatarUrl={blogger.avatarUrl} href={`#/blogger/${blogger.id}`} meta={meta} name={blogger.name} price={price} unit={unit} />;
}

function CampaignRow({ campaign }: { campaign: CampaignCardData }) {
  const { language, t } = useI18n();
  const businessName = campaign.businessName ?? campaign.business?.name ?? t("common.business");
  const meta = [businessName, campaign.city ? cityLabel(campaign.city, language) : null, campaign.deadline ? t("home.row.until", { date: formatShortDate(campaign.deadline, language) }) : null].filter(Boolean).join(" · ");
  const price = campaign.budgetFrom ? t("home.row.from", { amount: rowAmount(campaign.budgetFrom) }) : campaign.budgetTo ? t("home.row.upTo", { amount: rowAmount(campaign.budgetTo) }) : null;
  return <HomeRow avatarName={businessName} avatarUrl={campaign.businessAvatarUrl ?? campaign.business?.avatarUrl} href={`#/campaign/${campaign.id}`} meta={meta} name={campaign.title} neutral price={price} unit={price ? t("currency.uzs") : undefined} />;
}

function BrandFaceRow({ profile }: { profile: BrandFaceRow }) {
  const { language, t } = useI18n();
  const meta = [cityLabel(profile.city, language), ...profile.categories.slice(0, 2).map((category) => categoryLabel(category, language))].join(", ");
  return <HomeRow avatarName={profile.name} avatarUrl={profile.avatarUrl} href={`#/brand-face-detail/${profile.id}`} meta={meta} name={profile.name} price={profile.collaborationPrice ? rowAmount(profile.collaborationPrice) : null} unit={t("home.row.collaboration")} />;
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

// The lapis hero (D62): who is signed in, the question of the day, the search and the quick chips. Search is the
// main action of Home: Business looks for creators, creators look for campaigns.
function HomeHero({ role, categories, me }: { role: HomeRole; categories: string[]; me: OwnProfile | null }) {
  const { language, t } = useI18n();
  const [value, setValue] = useState("");
  const findsCreators = role === "Business";
  const route = findsCreators ? "/search" : "/campaigns";
  const submit = (event: FormEvent) => {
    event.preventDefault();
    const query = value.trim();
    window.location.hash = query ? `${route}?q=${encodeURIComponent(query)}` : route;
  };
  const meLine = me ? [me.name, me.city ? cityLabel(me.city, language) : null].filter(Boolean).join(", ") : null;
  return <section aria-labelledby="home-search-title" className="home-hero">
    <SeasonArt variant="static" />
    <p className="home-hero__me">{meLine ?? " "}</p>
    <h2 className="home-hero__title" id="home-search-title">{t(findsCreators ? "home.searchCreatorsTitle" : "home.searchCampaignsTitle")}</h2>
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

// "Waiting for your decision": only real, actionable items from existing endpoints; hidden when there is nothing to do.
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
  // A cold start draws the Home remembered from the last visit at once; the fresh answer replaces it quietly.
  const [data, setData] = useState<HomeData | null>(() => initialData ?? (isPreview ? null : readHomeSnapshot(resolvedRole)));
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
        writeHomeSnapshot(resolvedRole, response);
      })
      .catch(() => {
        if (controller.signal.aborted || version !== requestVersion.current) return;
        setOffline(typeof navigator !== "undefined" && navigator.onLine === false);
        setFailed(true);
      });
  }, [isPreview, resolvedRole]);

  useEffect(() => {
    if (isPreview) return;
    load();
    return () => activeRequest.current?.abort();
  }, [isPreview, load]);
  useProfileDataRefresh(load);
  const [activityReloadKey, setActivityReloadKey] = useState(0);
  useScreenRefresh(() => { setActivityReloadKey((key) => key + 1); load(); });

  const activity = useHomeActivity(resolvedRole, !isPreview, activityReloadKey);
  const forYou = useRecommendations(!isPreview, activityReloadKey);
  const me = useOwnProfile(resolvedRole, !isPreview, activityReloadKey);
  const forYouCount = forYou ? forYou.campaigns.length + forYou.bloggers.length + forYou.brandFaces.length : 0;
  const forYouRail = forYou && forYouCount > 0 && <HomeSection actionHref={forYou.audience === "business" ? "#/search" : "#/campaigns"} title={t(forYou.audience === "business" ? "home.forYouBusiness" : "home.forYou")}>
    {forYou.campaigns.map((campaign) => <CampaignRow campaign={campaign} key={campaign.id} />)}
    {forYou.bloggers.map((blogger) => <BloggerRow blogger={blogger} key={blogger.id} />)}
    {forYou.brandFaces.map((profile) => <BrandFaceRow key={profile.id} profile={profile} />)}
  </HomeSection>;
  const hasAnyBlogger = Boolean(data?.promotedBloggers.length || data?.topRatedBloggers.length || data?.newBloggers.length);
  const hasCampaigns = Boolean(data?.promotedCampaigns.length);
  const bloggerRail = (title: string, bloggers: HomeData["topRatedBloggers"]) => bloggers.length > 0 && <HomeSection actionHref="#/search" title={title}>{bloggers.map((blogger) => <BloggerRow blogger={blogger} key={blogger.id} />)}</HomeSection>;
  const brandFaceRail = data && data.newBrandFaces.length > 0 && <HomeSection actionHref="#/search?type=brand-face" title={t("home.newBrandFaces")}>{data.newBrandFaces.map((profile) => <BrandFaceRow key={profile.id} profile={profile} />)}</HomeSection>;
  const campaignRail = data && data.promotedCampaigns.length > 0 && <HomeSection actionHref="#/campaigns" title={t("home.promotedCampaigns")}>{data.promotedCampaigns.map((campaign) => <CampaignRow campaign={campaign} key={campaign.id} />)}</HomeSection>;

  return <div className="home screen screen--with-nav">
    <HomeHero categories={data?.categories ?? []} me={me} role={resolvedRole} />
    <AtlasStrip />
    <SeasonNote />
    <div className="home-top">
      {activity && activity.length > 0 && <HomeActivity items={activity} />}
      {me?.progress.next && <HomeProgress progress={me.progress} />}
    </div>
    {failed && !data ? <HomeError offline={offline} onRetry={load} /> : !data ? <HomeSkeleton role={resolvedRole} /> : <div className="home-content">
      {failed && <p className="text-sm text-brand-muted" role="status">{t("common.connectionRetry")}</p>}
      {forYouRail}
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
  const rows = role === "Business" ? 3 : 2;
  return <div aria-busy="true" aria-label={t("home.loading")} className="home-skeleton">
    <span className="sr-only">{t("home.loading")}</span>
    <section><Skeleton className="h-6 w-40" /><div className="home-skeleton__list">{Array.from({ length: rows }, (_, item) => <Skeleton className="h-20" key={item} />)}</div></section>
    <section><Skeleton className="h-6 w-48" /><div className="home-skeleton__list"><Skeleton className="h-20" /><Skeleton className="h-20" /></div></section>
  </div>;
}
