import { useEffect, useState } from "react";
import { getBlogger, getBloggerReviews, getPublicContact, type BloggerDetails, type BloggerReview } from "../api/marketplace";
import { Avatar, Badge, BottomNav, Button, Card, ErrorState, Icon, LoadingState, Rating, StatsCard, Toast } from "../components/ui";
import { categoryLabel, cityLabel, useI18n } from "../i18n";
import { formatCurrency } from "../lib/currency";
import { FavoriteButton } from "../components/FavoriteButton";
import { ContactList, hasContacts } from "../components/ContactList";

type Contact = { phone?: string | null; email?: string | null };

export function BloggerDetails({ id }: { id: string }) {
  const { language, t } = useI18n();
  const [blogger, setBlogger] = useState<BloggerDetails | null>(null);
  const [loading, setLoading] = useState(true);
  const [failed, setFailed] = useState(false);
  const [contact, setContact] = useState<Contact | null>(null);
  const [reviews, setReviews] = useState<BloggerReview[]>([]);
  const [toast, setToast] = useState("");

  const loadBlogger = () => {
    setLoading(true);
    setFailed(false);
    getBlogger(id).then(setBlogger).catch(() => { setBlogger(null); setFailed(true); }).finally(() => setLoading(false));
  };

  useEffect(loadBlogger, [id]);

  useEffect(() => {
    getBloggerReviews(id).then(setReviews).catch(() => undefined);
  }, [id]);

  useEffect(() => {
    getPublicContact("Blogger", id)
      .then(setContact)
      .catch(() => undefined);
  }, [id]);

  if (loading) return <div className="screen"><LoadingState title={t("common.loadingProfile")} /></div>;
  if (failed || !blogger) return <div className="screen"><ErrorState onRetry={loadBlogger} subtitle={t("common.connectionRetry")} title={t("common.openFailed")} /></div>;

  const socialContact = (type: string, kind: "instagram" | "tiktok" | "youtube" | "telegram") => blogger.platforms.find((platform) => platform.type.toLowerCase() === type)?.url ? { kind, value: blogger.platforms.find((platform) => platform.type.toLowerCase() === type)!.url } : null;
  const contacts = [
    contact?.phone ? { kind: "phone" as const, value: contact.phone } : null,
    contact?.telegram ? { kind: "telegram" as const, value: contact.telegram } : null,
    socialContact("instagram", "instagram"),
    socialContact("tiktok", "tiktok"),
    socialContact("youtube", "youtube"),
    contact?.email ? { kind: "email" as const, value: contact.email } : null
  ].filter((item): item is NonNullable<typeof item> => item !== null);
  const portfolio = blogger.portfolioItems;
  return <div className="screen pb-36">
    <div className="relative -mx-5 h-48 overflow-hidden bg-gradient-to-br from-violet-200 via-blue-100 to-cyan-100">
      {blogger.coverUrl && <img alt="" className="h-full w-full object-cover opacity-35" src={blogger.coverUrl} />}
      <div className="absolute inset-0 bg-gradient-to-b from-white/20 to-white/80" /><a className="absolute left-4 top-4 grid h-11 w-11 place-items-center rounded-full bg-white/85 shadow-card" href="#/search"><Icon name="back" /></a><FavoriteButton bloggerId={blogger.id} className="absolute right-4 top-4" />
    </div>
    <div className="relative -mt-16 text-center"><div className="mx-auto w-fit"><Avatar name={blogger.name} size="xl" src={blogger.avatarUrl} verified={blogger.verified} /></div><h1 className="mt-3 text-2xl font-extrabold tracking-tight">{blogger.name}</h1><p className="mt-1 text-sm text-brand-muted">{blogger.categories.map((category) => categoryLabel(category, language)).join(" · ")} · {cityLabel(blogger.city, language)}</p><div className="mt-2"><Rating count={blogger.reviewsCount} value={blogger.rating} /> <span className="text-sm text-brand-muted">· {t("details.deals", { count: blogger.completedDealsCount })}</span></div></div>
    <div className="mt-5 grid grid-cols-3 gap-2"><StatsCard label={t("details.followers")} value={`${Math.round(blogger.totalFollowers / 1000)}K`} /><StatsCard label="ER" value={`${blogger.engagementRate}%`} /><StatsCard label={t("details.completedDeals")} value={String(blogger.completedDealsCount)} /></div>
    <Card className="mt-5"><h2 className="font-extrabold">{t("details.about")}</h2><p className="mt-2 text-sm leading-6 text-brand-muted">{blogger.bio ?? t("details.filling")}</p><div className="mt-3 flex flex-wrap gap-2">{blogger.barterEnabled && <Badge tone="green">{t("card.barter")}</Badge>}{blogger.verified && <Badge tone="blue">{t("card.verified")}</Badge>}</div></Card>
    {portfolio.length > 0 && <section className="mt-5"><h2 className="mb-3 font-extrabold">{t("details.portfolio")}</h2><div className="no-scrollbar -mx-5 flex gap-3 overflow-x-auto px-5">{portfolio.map((item) => <a className="relative h-28 w-24 shrink-0 overflow-hidden rounded-2xl bg-slate-100" href={item.url} key={item.id} rel="noreferrer" target="_blank"><img alt={item.title} className="h-full w-full object-cover" src={item.url} />{item.type === "VIDEO" && <span aria-label={t("details.video")} className="absolute inset-0 grid place-items-center bg-slate-950/30 text-white">▶</span>}</a>)}</div></section>}
    <section className="mt-5"><h2 className="mb-3 font-extrabold">{t("details.reviews")}</h2>{reviews.length ? <div className="grid gap-2">{reviews.map((review) => <Card className="p-3" key={review.id}><div className="flex items-center justify-between"><Rating value={review.rating} /><span className="text-xs text-brand-muted">{new Intl.DateTimeFormat(language === "uz" ? "uz-UZ" : "ru-RU", { day: "numeric", month: "short", year: "numeric" }).format(new Date(review.createdAtUtc))}</span></div>{review.reviewerName && <p className="mt-2 text-sm font-bold">{review.reviewerName}</p>}{review.comment && <p className="mt-1 text-sm leading-5 text-brand-muted">{review.comment}</p>}</Card>)}</div> : <Card><p className="text-sm text-brand-muted">{t("details.noReviews")}</p></Card>}</section>
    <section className="mt-5"><h2 className="mb-3 font-extrabold">{t("details.adPrices")}</h2><div className="grid grid-cols-2 gap-2">{[[t("card.stories"), blogger.storiesPrice], [t("card.reels"), blogger.reelsPrice], [t("card.post"), blogger.postPrice], [t("card.integration"), blogger.integrationPrice]].map(([label, value]) => <Card className="p-3" key={String(label)}><p className="text-xs text-brand-muted">{label}</p><p className="mt-1 text-sm font-extrabold">{formatCurrency(Number(value))}</p></Card>)}</div></section>
    {hasContacts(contacts) && <section className="mt-5"><h2 className="mb-3 font-extrabold">{t("details.contacts")}</h2><ContactList items={contacts} /></section>}
    <div className="fixed inset-x-0 bottom-[70px] z-30 mx-auto max-w-[430px] bg-white/90 px-5 pb-3 pt-2 backdrop-blur"><a href="#/campaigns"><Button className="w-full"><Icon name="send" />{t("details.createCampaign")}</Button></a></div>
    <Toast message={toast} /><BottomNav />
  </div>;
}
