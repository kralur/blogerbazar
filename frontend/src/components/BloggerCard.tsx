import { formatCurrency, formatNumber, formatPercentage } from "../lib/currency";
import { categoryLabel, cityLabel, useI18n } from "../i18n";
import { Avatar } from "./ui";
import { FavoriteButton } from "./FavoriteButton";

export type BloggerCardData = {
  id: string;
  name: string;
  city: string;
  categories: string[];
  totalFollowers: number;
  priceFrom?: number | null;
  priceTo?: number | null;
  priceNote?: string | null;
  rating?: number | null;
  reviewsCount: number;
  completedDealsCount: number;
  avatarUrl?: string | null;
  verified?: boolean;
  averageReach?: number | null;
  engagementRate?: number | null;
  storiesPrice?: number | null;
  reelsPrice?: number | null;
  platform?: string | null;
  isPromoted?: boolean;
};

// One blogger card for Home rails, Search and Favorites; facts the API did not send are omitted, not shown as "—".
export function BloggerCard({ blogger, variant = "default" }: { blogger: BloggerCardData; variant?: "default" | "home" }) {
  const { t } = useI18n();
  const prices = [blogger.storiesPrice, blogger.reelsPrice, blogger.priceFrom].filter((value): value is number => value != null && value > 0);
  const priceFrom = prices.length > 0 ? formatCurrency(Math.min(...prices)) : null;
  return <article className={`catalog-blogger-card card-enter relative${variant === "home" ? " catalog-card--rail" : ""}`}><a aria-label={t("home.openBlogger", { name: blogger.name })} className="catalog-blogger-card__link" href={`#/blogger/${blogger.id}`}>
    <div className="catalog-blogger-card__identity">
      <Avatar name={blogger.name} size="sm" src={blogger.avatarUrl} variant="catalog" verified={blogger.verified} />
      <div className="min-w-0 flex-1"><div className="catalog-blogger-card__name-row"><strong>{blogger.name}</strong></div><p>{cityLabel(blogger.city)}{blogger.platform ? ` · ${blogger.platform}` : ""}</p></div>
    </div>
    <div className="catalog-blogger-card__categories">{blogger.isPromoted && <span className="catalog-card__promoted">{t("card.promoted")}</span>}{blogger.categories.slice(0, 2).map((category) => <span key={category}>{categoryLabel(category)}</span>)}</div>
    <div className="catalog-blogger-card__facts">
      <div><span>{t("common.followers")}</span><strong>{formatNumber(blogger.totalFollowers)}</strong></div>
      {blogger.engagementRate != null && <div><span>{t("search.er")}</span><strong>{formatPercentage(blogger.engagementRate)}</strong></div>}
      {priceFrom && <div><span>{t("card.priceFrom")}</span><strong>{priceFrom}</strong></div>}
    </div>
  </a><FavoriteButton bloggerId={blogger.id} className="absolute right-3 top-3" /></article>;
}
