import type { BrandFaceCatalogItem } from "../api/marketplace";
import { categoryLabel, cityLabel, useI18n } from "../i18n";
import { formatCurrency, formatNumber } from "../lib/currency";
import { Avatar } from "./ui";
import { FavoriteButton } from "./FavoriteButton";
import { spokenLanguageLabelKey } from "../lib/languages";
import { useIsDesktop } from "../lib/desktopLayout";
import { PersonCard } from "./PersonCard";

export function BrandFaceCard({ profile, onFavoriteChanged, variant = "default" }: { profile: Pick<BrandFaceCatalogItem, "id" | "name" | "city" | "categories" | "languages" | "collaborationPrice" | "avatarUrl" | "isPromoted"> & Partial<Pick<BrandFaceCatalogItem, "age" | "gender">>; onFavoriteChanged?: (isFavorite: boolean) => void; variant?: "default" | "home" }) {
  const { language, t } = useI18n();
  const categories = profile.categories.slice(0, 2);
  const languages = profile.languages.slice(0, 2);
  const desktop = useIsDesktop();
  const person = [profile.gender === "female" || profile.gender === "male" ? t(`brandFace.person.${profile.gender}`) : null, profile.age ? t("brandFace.ageYears", { count: profile.age }) : null].filter(Boolean).join(" · ");
  const spoken = languages.map((code) => spokenLanguageLabelKey(code) ? t(spokenLanguageLabelKey(code)!) : code).join(", ");

  // On a computer (the website) a brand face looks like a campaign in the catalog: a hanging tag, the price, city and
  // languages at the bottom.
  if (desktop && variant !== "home") {
    return <article className="catalog-tile">
      <a aria-label={t("search.openBrandFaceAria", { name: profile.name })} className="campaign-catalog-card" href={`#/brand-face-detail/${profile.id}`}>
        <div className="campaign-catalog-card__identity">
          <Avatar name={profile.name} size="sm" src={profile.avatarUrl} variant="catalog" />
          <div className="campaign-catalog-card__heading">
            <div className="campaign-catalog-card__name-row"><strong>{t("onboarding.brandFace")}</strong></div>
            <h2>{profile.name}</h2>
          </div>
        </div>
        {(profile.isPromoted || categories.length > 0) && <div className="campaign-catalog-card__categories">{profile.isPromoted && <span className="catalog-card__promoted">{t("search.promoted")}</span>}{categories.map((category) => <span key={category}>{categoryLabel(category, language)}</span>)}</div>}
        <p className="campaign-catalog-card__requirements">{[person, spoken].filter(Boolean).join(" · ") || "\u00a0"}</p>
        <dl className="campaign-catalog-card__facts">
          <div><dt>{t("common.city")}</dt><dd>{cityLabel(profile.city, language)}</dd></div>
          <div className="campaign-catalog-card__fact--budget"><dt>{t("common.price")}</dt><dd>{profile.collaborationPrice == null ? t("search.priceNotSpecified") : formatCurrency(profile.collaborationPrice)}</dd></div>
        </dl>
      </a>
      <FavoriteButton brandFaceId={profile.id} className="catalog-tile__save" onChanged={onFavoriteChanged} />
    </article>;
  }

  // In Telegram and on a phone a brand face is the same card as a blogger (owner, 2026-11-21: one card everywhere).
  return <PersonCard
    ariaLabel={t("search.openBrandFaceAria", { name: profile.name })}
    avatarUrl={profile.avatarUrl}
    facts={[person, cityLabel(profile.city, language), spoken].filter(Boolean).join(" · ")}
    href={`#/brand-face-detail/${profile.id}`}
    name={profile.name}
    price={profile.collaborationPrice == null ? null : formatNumber(profile.collaborationPrice)}
    priceLabel={t("common.price")}
    priceUnit={t("currency.uzs")}
    priority={variant === "home"}
    promotedLabel={profile.isPromoted ? t("search.promoted") : null}
    save={<FavoriteButton brandFaceId={profile.id} className="person-card__save" onChanged={onFavoriteChanged} />}
    tags={categories.map((category) => categoryLabel(category, language))}
  />;
}
