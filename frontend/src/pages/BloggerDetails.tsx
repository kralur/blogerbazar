import { useCallback, useEffect, useRef, useState } from "react";
import { getBlogger, getBloggerReviews, getCurrentPlatformUser, getPublicContact, normalizeMarketplaceRole, type BloggerDetails, type BloggerReview, type ContactDetails, type MarketplaceRole } from "../api/marketplace";
import { OfferForm } from "../components/OfferForm";
import { Avatar, BottomNav, Button, Card, ErrorState, FixedActionBar, Icon, LoadingState, Rating, Toast } from "../components/ui";
import { ChipList, DetailSection, FactGrid, ReviewList, type Fact } from "../components/details/DetailBlocks";
import { platformLabel } from "../lib/platforms";
import { categoryLabel, cityLabel, useI18n } from "../i18n";
import { formatCompactNumber, formatCurrency, formatPercentage } from "../lib/currency";
import { FavoriteButton } from "../components/FavoriteButton";
import { ContactList, hasContacts } from "../components/ContactList";
import { useTelegram } from "../telegram/TelegramProvider";
import { useProfileDataRefresh } from "../hooks/useProfileDataRefresh";
import { getCachedPublicDetail, setCachedPublicDetail } from "../data/publicDetailCache";
import { PageHeader } from "../components/PageHeader";

export function BloggerDetails({ id }: { id: string }) {
  const { language, t } = useI18n();
  const { openLink } = useTelegram();
  const [blogger, setBlogger] = useState<BloggerDetails | null>(() => getCachedPublicDetail<BloggerDetails>("blogger", id));
  const [loading, setLoading] = useState(() => !getCachedPublicDetail<BloggerDetails>("blogger", id));
  const [failed, setFailed] = useState(false);
  const [contact, setContact] = useState<ContactDetails | null>(null);
  const [reviews, setReviews] = useState<BloggerReview[]>([]);
  const [toast, setToast] = useState("");
  const [role, setRole] = useState<MarketplaceRole>();
  const [offerOpen, setOfferOpen] = useState(false);

  const requestIdRef = useRef(0);
  const loadBlogger = useCallback(() => {
    const cached = getCachedPublicDetail<BloggerDetails>("blogger", id);
    const requestId = ++requestIdRef.current;
    if (cached) setBlogger(cached);
    else setBlogger(null);
    setLoading(!cached);
    setFailed(false);
    getBlogger(id).then((response) => {
      if (requestId !== requestIdRef.current) return;
      setCachedPublicDetail("blogger", id, response);
      setBlogger(response);
    }).catch(() => {
      if (requestId !== requestIdRef.current) return;
      setFailed(true);
      if (!cached) setBlogger(null);
    }).finally(() => {
      if (requestId === requestIdRef.current) setLoading(false);
    });
  }, [id]);

  useEffect(() => {
    loadBlogger();
    return () => { requestIdRef.current += 1; };
  }, [loadBlogger]);
  useProfileDataRefresh(loadBlogger);

  useEffect(() => {
    getBloggerReviews(id).then(setReviews).catch(() => undefined);
  }, [id]);

  useEffect(() => {
    let active = true;
    getCurrentPlatformUser().then((user) => { if (active) setRole(normalizeMarketplaceRole(user.selectedMarketplaceRole)); }).catch(() => undefined);
    return () => { active = false; };
  }, []);

  useEffect(() => {
    getPublicContact("Blogger", id)
      .then(setContact)
      .catch(() => undefined);
  }, [id]);

  if (loading) return <div className="screen screen--with-nav"><LoadingState title={t("common.loadingProfile")} /><BottomNav /></div>;
  if (!blogger) return <div className="screen screen--with-nav"><ErrorState onRetry={loadBlogger} subtitle={t("common.connectionRetry")} title={t("common.openFailed")} /><BottomNav /></div>;

  const socialContact = (type: string, kind: "instagram" | "tiktok" | "youtube" | "telegram") => {
    const platform = blogger.platforms.find((item) => item.type.toLowerCase() === type);
    return platform?.url ? { kind, value: platform.url } : null;
  };
  const contacts = [
    contact?.phone ? { kind: "phone" as const, value: contact.phone } : null,
    contact?.telegram ? { kind: "telegram" as const, value: contact.telegram } : null,
    socialContact("instagram", "instagram"),
    socialContact("tiktok", "tiktok"),
    socialContact("youtube", "youtube"),
    contact?.email ? { kind: "email" as const, value: contact.email } : null
  ].filter((item): item is NonNullable<typeof item> => item !== null);
  const portfolio = blogger.portfolioItems;
  const prices: Fact[] = [
    { label: t("card.stories"), value: positiveCurrency(blogger.storiesPrice) },
    { label: t("card.reels"), value: positiveCurrency(blogger.reelsPrice) },
    { label: t("card.post"), value: positiveCurrency(blogger.postPrice) },
    { label: t("card.integration"), value: positiveCurrency(blogger.integrationPrice) }
  ];
  const hasPrices = prices.some((price) => price.value);
  const tags = [blogger.verified ? t("card.verified") : null, blogger.barterEnabled ? t("card.barter") : null].filter((tag): tag is string => tag !== null);
  return <div className="screen screen--with-nav">
    <PageHeader actions={<FavoriteButton bloggerId={blogger.id} />} back={{ href: "#/search", label: t("common.back") }} />
    {blogger.coverUrl && <div className="profile-cover"><img alt="" className="image-fade h-full w-full object-cover" decoding="async" src={blogger.coverUrl} /></div>}
    <div className={`relative text-center ${blogger.coverUrl ? "-mt-14" : "mt-2"}`}><div className="mx-auto w-fit"><Avatar name={blogger.name} size="xl" src={blogger.avatarUrl} verified={blogger.verified} /></div><h1 className="mt-3 text-2xl font-extrabold tracking-tight">{blogger.name}</h1><p className="mt-1 text-sm text-brand-muted">{blogger.categories.map((category) => categoryLabel(category, language)).join(" · ")} · {cityLabel(blogger.city, language)}</p><div className="mt-2"><Rating count={blogger.reviewsCount} value={blogger.rating} /> <span className="text-sm text-brand-muted">· {t("details.deals", { count: blogger.completedDealsCount })}</span></div></div>
    <FactGrid className="mt-5 fact-grid--three" facts={[
      { label: t("details.followers"), value: formatCompactNumber(blogger.totalFollowers) },
      { label: t("search.er"), value: blogger.engagementRate ? formatPercentage(blogger.engagementRate) : null },
      { label: t("details.reach"), value: blogger.averageReach ? formatCompactNumber(blogger.averageReach) : null }
    ]} />
    {failed && <p className="mt-3 text-sm text-brand-muted" role="status">{t("common.connectionRetry")}</p>}
    <DetailSection title={t("details.adPrices")}>{hasPrices ? <FactGrid facts={prices} /> : <Card><p className="text-sm text-brand-muted">{t("details.pricesOnRequest")}</p></Card>}</DetailSection>
    <DetailSection title={t("details.about")}><Card><p className="text-sm leading-6 text-brand-muted">{blogger.bio ?? t("details.filling")}</p>{tags.length > 0 && <div className="mt-3"><ChipList items={tags} /></div>}</Card></DetailSection>
    {blogger.platforms.length > 0 && <DetailSection title={t("details.platforms")}><FactGrid facts={blogger.platforms.map((platform) => ({ label: platformLabel(platform.type, t), value: platform.followers ? formatCompactNumber(platform.followers) : t("card.onRequest") }))} /></DetailSection>}
    {portfolio.length > 0 && <DetailSection title={t("details.portfolio")}><div className="no-scrollbar -mx-4 flex gap-3 overflow-x-auto px-4">{portfolio.map((item) => <a className="relative h-28 w-24 shrink-0 overflow-hidden rounded-2xl bg-brand-soft" href={item.url} key={item.id} onClick={(event) => { event.preventDefault(); openLink(item.url); }}><img alt={item.title} className="image-fade h-full w-full object-cover" decoding="async" loading="lazy" src={item.url} />{item.type === "VIDEO" && <span aria-label={t("details.video")} className="absolute inset-0 grid place-items-center bg-slate-950/30 text-white">▶</span>}</a>)}</div></DetailSection>}
    <DetailSection title={t("details.reviews")}><ReviewList emptyText={t("details.noReviews")} reviews={reviews} /></DetailSection>
    {hasContacts(contacts) && <DetailSection title={t("details.contacts")}><ContactList items={contacts} /></DetailSection>}
    {role === "Business" && <FixedActionBar><Button className="w-full" onClick={() => setOfferOpen(true)} type="button"><Icon name="send" />{t("offers.propose")}</Button></FixedActionBar>}
    <OfferForm bloggerId={blogger.id} onClose={() => setOfferOpen(false)} onSent={() => { setOfferOpen(false); setToast(t("offers.sent")); }} open={offerOpen} />
    <Toast message={toast} /><BottomNav />
  </div>;
}

function positiveCurrency(value?: number | null) {
  return value != null && value > 0 ? formatCurrency(value) : null;
}
