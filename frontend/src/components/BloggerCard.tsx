import { formatCurrency, formatNumber, formatPercentage } from "../lib/currency";
import { categoryLabel, cityLabel, useI18n } from "../i18n";
import { Avatar, Price } from "./ui";
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

export function BloggerCard({ blogger, variant = "default" }: { blogger: BloggerCardData; variant?: "default" | "home" }) {
  const { t } = useI18n();
  const primaryPrice = blogger.storiesPrice ?? blogger.priceFrom;
  if (variant === "home") {
    return <article className="home-blogger-card card-enter relative overflow-hidden">
      <a aria-label={t("home.openBlogger", { name: blogger.name })} className="block" href={`#/blogger/${blogger.id}`}>
        <div className="flex min-w-0 items-start gap-3">
          <Avatar name={blogger.name} size="sm" src={blogger.avatarUrl} variant="home" verified={blogger.verified} />
          <div className="min-w-0 flex-1">
            <div className="flex min-w-0 items-center gap-2"><strong className="truncate text-[15px] font-extrabold tracking-tight">{blogger.name}</strong>{blogger.isPromoted && <span className="home-card__badge">{t("card.promoted")}</span>}</div>
            <p className="mt-1 truncate text-xs text-[color:var(--bb-text-secondary)]">{cityLabel(blogger.city)}{blogger.platform ? ` · ${blogger.platform}` : ""}</p>
          </div>
        </div>
        <div className="mt-3 flex flex-wrap gap-1.5">{blogger.categories.slice(0, 2).map((category) => <span className="home-card__chip" key={category}>{categoryLabel(category)}</span>)}</div>
        <div className="home-blogger-card__metrics mt-4 grid grid-cols-2 gap-2">
          <div><span>{t("common.followers")}</span><strong>{formatNumber(blogger.totalFollowers)}</strong></div>
          <div><span>{t("card.stories")}</span><strong>{primaryPrice == null ? t("card.onRequest") : formatCurrency(primaryPrice)}</strong></div>
        </div>
      </a>
      <FavoriteButton bloggerId={blogger.id} className="absolute right-3 top-3 home-favorite-button" />
    </article>;
  }
  return <article className="catalog-blogger-card card-enter relative"><a aria-label={t("home.openBlogger", { name: blogger.name })} className="catalog-blogger-card__link" href={`#/blogger/${blogger.id}`}>
    <div className="catalog-blogger-card__identity">
      <Avatar name={blogger.name} size="sm" src={blogger.avatarUrl} variant="catalog" verified={blogger.verified} />
      <div className="min-w-0 flex-1"><div className="catalog-blogger-card__name-row"><strong>{blogger.name}</strong>{blogger.isPromoted && <span className="catalog-blogger-card__promotion">{t("card.promoted")}</span>}</div><p>{cityLabel(blogger.city)}{blogger.platform ? ` · ${blogger.platform}` : ""}</p><div className="catalog-blogger-card__categories">{blogger.categories.slice(0, 2).map((category) => <span key={category}>{categoryLabel(category)}</span>)}</div></div>
    </div>
    <div className="catalog-blogger-card__facts"><div><span>{t("common.followers")}</span><strong>{formatNumber(blogger.totalFollowers)}</strong></div><div><span>{t("search.er")}</span><strong>{blogger.engagementRate == null ? "—" : formatPercentage(blogger.engagementRate)}</strong></div><div><span>{t("common.price")}</span><Price value={primaryPrice} /></div></div>
  </a><FavoriteButton bloggerId={blogger.id} className="absolute right-3 top-3" /></article>;
}
