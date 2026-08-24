import type { BrandFaceCatalogItem } from "../api/marketplace";
import { categoryLabel, cityLabel, useI18n } from "../i18n";
import { formatCurrency } from "../lib/currency";
import { Avatar } from "./ui";
import { FavoriteButton } from "./FavoriteButton";

export function BrandFaceCard({ profile, onFavoriteChanged }: { profile: Pick<BrandFaceCatalogItem, "id" | "name" | "city" | "categories" | "languages" | "collaborationPrice" | "avatarUrl" | "isPromoted">; onFavoriteChanged?: (isFavorite: boolean) => void }) {
  const { language, t } = useI18n();
  const categories = profile.categories.slice(0, 2);
  const languages = profile.languages.slice(0, 2);

  return <article className="catalog-brand-face-card">
    <a aria-label={t("search.openBrandFaceAria", { name: profile.name })} className="catalog-brand-face-card__link" href={`#/brand-face-detail/${profile.id}`}>
      <div className="catalog-brand-face-card__identity">
        <Avatar name={profile.name} size="md" src={profile.avatarUrl} variant="catalog" />
        <div className="catalog-brand-face-card__heading">
          <div className="catalog-brand-face-card__name-row">
            <strong>{profile.name}</strong>
            {profile.isPromoted && <span className="catalog-brand-face-card__promotion">{t("search.promoted")}</span>}
          </div>
          <p>{cityLabel(profile.city, language)}</p>
        </div>
      </div>
      {(categories.length > 0 || languages.length > 0) && <div className="catalog-brand-face-card__chips">
        {categories.map((category) => <span key={`category:${category}`}>{categoryLabel(category, language)}</span>)}
        {languages.map((language) => <span key={`language:${language}`}>{language}</span>)}
      </div>}
      <div className="catalog-brand-face-card__price">
        <span>{t("common.price")}</span>
        <strong>{profile.collaborationPrice == null ? t("search.priceNotSpecified") : formatCurrency(profile.collaborationPrice)}</strong>
      </div>
    </a>
    <FavoriteButton brandFaceId={profile.id} className="catalog-brand-face-card__favorite" onChanged={onFavoriteChanged} />
  </article>;
}
