import { useCallback, useEffect, useState } from "react";
import { getBlogger, getBloggerReviews, getPublicContact, type BloggerDetails, type BloggerReview, type ContactDetails } from "../api/marketplace";
import { Avatar, Badge, BottomNav, Button, Card, ErrorState, FixedActionBar, Icon, LoadingState, Rating, StatsCard, Toast } from "../components/ui";
import { categoryLabel, cityLabel, useI18n } from "../i18n";
import { formatCurrency } from "../lib/currency";
import { FavoriteButton } from "../components/FavoriteButton";
import { ContactList, hasContacts } from "../components/ContactList";
import { useTelegram } from "../telegram/TelegramProvider";
import { useProfileDataRefresh } from "../hooks/useProfileDataRefresh";

export function BloggerDetails({ id }: { id: string }) {
  const { language, t } = useI18n();
  const { openLink } = useTelegram();
  const [blogger, setBlogger] = useState<BloggerDetails | null>(null);
  const [loading, setLoading] = useState(true);
  const [failed, setFailed] = useState(false);
  const [contact, setContact] = useState<ContactDetails | null>(null);
  const [reviews, setReviews] = useState<BloggerReview[]>([]);
  const [toast, setToast] = useState("");

  const loadBlogger = useCallback(() => {
    setLoading(true);
    setFailed(false);
    getBlogger(id).then(setBlogger).catch(() => { setBlogger(null); setFailed(true); }).finally(() => setLoading(false));
  }, [id]);

  useEffect(() => { loadBlogger(); }, [loadBlogger]);
  useProfileDataRefresh(loadBlogger);

  useEffect(() => {
    getBloggerReviews(id).then(setReviews).catch(() => undefined);
  }, [id]);

  useEffect(() => {
    getPublicContact("Blogger", id)
      .then(setContact)
      .catch(() => undefined);
  }, [id]);

  if (loading) return <div className="screen screen--with-nav"><LoadingState title={t("common.loadingProfile")} /><BottomNav /></div>;
  if (failed || !blogger) return <div className="screen screen--with-nav"><ErrorState onRetry={loadBlogger} subtitle={t("common.connectionRetry")} title={t("common.openFailed")} /><BottomNav /></div>;

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
  return <div className="screen screen--with-nav">
    <div className="relative -mx-5 h-48 overflow-hidden bg-gradient-to-br from-violet-200 via-blue-100 to-cyan-100">
      {blogger.coverUrl && <img alt="" className="image-fade h-full w-full object-cover opacity-35" decoding="async" src={blogger.coverUrl} />}
      <div className="absolute inset-0 bg-gradient-to-b from-white/20 to-white/80" /><a className="absolute left-4 top-4 grid h-11 w-11 place-items-center rounded-full bg-white/85 shadow-card" href="#/search"><Icon name="back" /></a><FavoriteButton bloggerId={blogger.id} className="absolute right-4 top-4" />
    </div>
    <div className="relative -mt-16 text-center"><div className="mx-auto w-fit"><Avatar name={blogger.name} size="xl" src={blogger.avatarUrl} verified={blogger.verified} /></div><h1 className="mt-3 text-2xl font-extrabold tracking-tight">{blogger.name}</h1><p className="mt-1 text-sm text-brand-muted">{blogger.categories.map((category) => categoryLabel(category, language)).join(" · ")} · {cityLabel(blogger.city, language)}</p><div className="mt-2"><Rating count={blogger.reviewsCount} value={blogger.rating} /> <span className="text-sm text-brand-muted">· {t("details.deals", { count: blogger.completedDealsCount })}</span></div></div>
    <div className="mt-5 grid grid-cols-3 gap-2"><StatsCard label={t("details.followers")} value={`${Math.round(blogger.totalFollowers / 1000)}K`} /><StatsCard label="ER" value={`${blogger.engagementRate}%`} /><StatsCard label={t("details.completedDeals")} value={String(blogger.completedDealsCount)} /></div>
    <Card className="mt-5"><h2 className="font-extrabold">{t("details.about")}</h2><p className="mt-2 text-sm leading-6 text-brand-muted">{blogger.bio ?? t("details.filling")}</p><div className="mt-3 flex flex-wrap gap-2">{blogger.barterEnabled && <Badge tone="green">{t("card.barter")}</Badge>}{blogger.verified && <Badge tone="blue">{t("card.verified")}</Badge>}</div></Card>
    {portfolio.length > 0 && <section className="mt-5"><h2 className="mb-3 font-extrabold">{t("details.portfolio")}</h2><div className="no-scrollbar -mx-5 flex gap-3 overflow-x-auto px-5">{portfolio.map((item) => <a className="relative h-28 w-24 shrink-0 overflow-hidden rounded-2xl bg-slate-100" href={item.url} key={item.id} onClick={(event) => { event.preventDefault(); openLink(item.url); }}><img alt={item.title} className="image-fade h-full w-full object-cover" decoding="async" loading="lazy" src={item.url} />{item.type === "VIDEO" && <span aria-label={t("details.video")} className="absolute inset-0 grid place-items-center bg-slate-950/30 text-white">▶</span>}</a>)}</div></section>}
    <section className="mt-5"><h2 className="mb-3 font-extrabold">{t("details.reviews")}</h2>{reviews.length ? <div className="grid gap-2">{reviews.map((review) => <Card className="p-3" key={review.id}><div className="flex items-center justify-between"><Rating value={review.rating} /><span className="text-xs text-brand-muted">{new Intl.DateTimeFormat(language === "uz" ? "uz-UZ" : "ru-RU", { day: "numeric", month: "short", year: "numeric" }).format(new Date(review.createdAtUtc))}</span></div>{review.reviewerName && <p className="mt-2 text-sm font-bold">{review.reviewerName}</p>}{review.comment && <p className="mt-1 text-sm leading-5 text-brand-muted">{review.comment}</p>}</Card>)}</div> : <Card><p className="text-sm text-brand-muted">{t("details.noReviews")}</p></Card>}</section>
    <section className="mt-5"><h2 className="mb-3 font-extrabold">{t("details.adPrices")}</h2><div className="grid grid-cols-2 gap-2">{[[t("card.stories"), blogger.storiesPrice], [t("card.reels"), blogger.reelsPrice], [t("card.post"), blogger.postPrice], [t("card.integration"), blogger.integrationPrice]].map(([label, value]) => <Card className="p-3" key={String(label)}><p className="text-xs text-brand-muted">{label}</p><p className="mt-1 text-sm font-extrabold">{formatCurrency(Number(value))}</p></Card>)}</div></section>
    {hasContacts(contacts) && <section className="mt-5"><h2 className="mb-3 font-extrabold">{t("details.contacts")}</h2><ContactList items={contacts} /></section>}
    <FixedActionBar><a href="#/campaigns"><Button className="w-full"><Icon name="send" />{t("details.createCampaign")}</Button></a></FixedActionBar>
    <Toast message={toast} /><BottomNav />
  </div>;
}
