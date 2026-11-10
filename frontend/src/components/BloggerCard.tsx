import { formatCompactNumber, formatNumber, formatPercentage } from "../lib/currency";
import { categoryLabel, cityLabel, useI18n } from "../i18n";
import { Avatar } from "./ui";
import { FavoriteButton } from "./FavoriteButton";
import { platformLabel } from "../lib/platforms";
import { useIsDesktop } from "../lib/desktopLayout";

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
  const desktop = useIsDesktop();
  const prices = [blogger.storiesPrice, blogger.reelsPrice, blogger.priceFrom].filter((value): value is number => value != null && value > 0);
  const priceFrom = prices.length > 0 ? Math.min(...prices) : null;
  const facts = [cityLabel(blogger.city), blogger.platform ? platformLabel(blogger.platform, t) : null, `${t("common.followers")} ${blogger.totalFollowers >= 1_000_000 ? formatCompactNumber(blogger.totalFollowers) : formatNumber(blogger.totalFollowers)}`, blogger.engagementRate != null ? `${t("search.er")} ${formatPercentage(blogger.engagementRate)}` : null].filter(Boolean).join(" · ");
  // On a computer (the website) the catalog shows the blogger the way it shows a campaign: a hanging tag with the name,
  // categories, the reach in a line, and the price, city and ER at the bottom.
  if (desktop && variant !== "home") {
    const reach = [`${t("common.followers")} ${blogger.totalFollowers >= 1_000_000 ? formatCompactNumber(blogger.totalFollowers) : formatNumber(blogger.totalFollowers)}`, blogger.averageReach ? `${t("details.reach")} ${formatCompactNumber(blogger.averageReach)}` : null].filter(Boolean).join(" · ");
    return <article className="catalog-tile card-enter">
      <a aria-label={t("home.openBlogger", { name: blogger.name })} className="campaign-catalog-card" href={`#/blogger/${blogger.id}`}>
        <div className="campaign-catalog-card__identity">
          <Avatar name={blogger.name} size="sm" src={blogger.avatarUrl} variant="catalog" />
          <div className="campaign-catalog-card__heading">
            <div className="campaign-catalog-card__name-row"><strong>{blogger.platform ? platformLabel(blogger.platform, t) : t("profile.blogger")}</strong></div>
            <h2>{blogger.name}</h2>
          </div>
        </div>
        {(blogger.isPromoted || blogger.categories.length > 0) && <div className="campaign-catalog-card__categories">{blogger.isPromoted && <span className="catalog-card__promoted">{t("card.promoted")}</span>}{blogger.categories.slice(0, 2).map((category) => <span key={category}>{categoryLabel(category)}</span>)}</div>}
        <p className="campaign-catalog-card__requirements">{reach}</p>
        <dl className="campaign-catalog-card__facts">
          <div><dt>{t("common.city")}</dt><dd>{cityLabel(blogger.city)}</dd></div>
          {priceFrom != null && <div className="campaign-catalog-card__fact--budget"><dt>{t("card.priceFrom")}</dt><dd>{formatNumber(priceFrom)} {t("currency.uzs")}</dd></div>}
          {blogger.engagementRate != null && <div><dt>{t("search.er")}</dt><dd>{formatPercentage(blogger.engagementRate)}</dd></div>}
        </dl>
      </a>
      <FavoriteButton bloggerId={blogger.id} className="catalog-tile__save" />
    </article>;
  }

  // D63: one row per blogger, like the Home lists: photo, name, the facts in one line, the price on the right.
  return <article className={`catalog-blogger-card card-enter relative${variant === "home" ? " catalog-card--rail" : ""}`}><a aria-label={t("home.openBlogger", { name: blogger.name })} className="catalog-blogger-card__link" href={`#/blogger/${blogger.id}`}>
    <Avatar name={blogger.name} priority={variant === "home"} size="sm" src={blogger.avatarUrl} variant="catalog" />
    <div className="catalog-blogger-card__body">
      <div className="catalog-blogger-card__name-row"><strong>{blogger.name}</strong></div>
      <p className="catalog-blogger-card__meta">{facts}</p>
      {(blogger.isPromoted || blogger.categories.length > 0) && <div className="catalog-blogger-card__categories">{blogger.isPromoted && <span className="catalog-card__promoted">{t("card.promoted")}</span>}{blogger.categories.slice(0, 2).map((category) => <span key={category}>{categoryLabel(category)}</span>)}</div>}
    </div>
    {priceFrom != null && <div className="catalog-blogger-card__price"><span>{t("card.priceFrom")}</span><strong>{formatNumber(priceFrom)}</strong><span>{t("currency.uzs")}</span></div>}
  </a><FavoriteButton bloggerId={blogger.id} className="catalog-blogger-card__save" /></article>;
}
