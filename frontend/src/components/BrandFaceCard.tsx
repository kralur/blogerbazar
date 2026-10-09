import type { BrandFaceCatalogItem } from "../api/marketplace";
import { categoryLabel, cityLabel, useI18n } from "../i18n";
import { formatCurrency } from "../lib/currency";
import { Avatar } from "./ui";
import { FavoriteButton } from "./FavoriteButton";
import { spokenLanguageLabelKey } from "../lib/languages";

export function BrandFaceCard({ profile, onFavoriteChanged, variant = "default" }: { profile: Pick<BrandFaceCatalogItem, "id" | "name" | "city" | "categories" | "languages" | "collaborationPrice" | "avatarUrl" | "isPromoted"> & Partial<Pick<BrandFaceCatalogItem, "age" | "gender">>; onFavoriteChanged?: (isFavorite: boolean) => void; variant?: "default" | "home" }) {
  const { language, t } = useI18n();
  const categories = profile.categories.slice(0, 2);
  const languages = profile.languages.slice(0, 2);

  return <article className={`catalog-brand-face-card${variant === "home" ? " catalog-card--rail" : ""}`}>
    <a aria-label={t("search.openBrandFaceAria", { name: profile.name })} className="catalog-brand-face-card__link" href={`#/brand-face-detail/${profile.id}`}>
      <div className="catalog-brand-face-card__identity">
        <Avatar name={profile.name} size="md" src={profile.avatarUrl} variant="catalog" />
        <div className="catalog-brand-face-card__heading">
          <div className="catalog-brand-face-card__name-row">
            <strong>{profile.name}</strong>
          </div>
          <p>{[profile.gender === "female" || profile.gender === "male" ? t(`brandFace.person.${profile.gender}`) : null, profile.age ? t("brandFace.ageYears", { count: profile.age }) : null, cityLabel(profile.city, language)].filter(Boolean).join(" · ")}</p>
        </div>
      </div>
      {(profile.isPromoted || categories.length > 0 || languages.length > 0) && <div className="catalog-brand-face-card__chips">
        {profile.isPromoted && <span className="catalog-card__promoted">{t("search.promoted")}</span>}
        {categories.map((category) => <span key={`category:${category}`}>{categoryLabel(category, language)}</span>)}
        {languages.map((code) => <span key={`language:${code}`}>{spokenLanguageLabelKey(code) ? t(spokenLanguageLabelKey(code)!) : code}</span>)}
      </div>}
      <div className="catalog-brand-face-card__price">
        <span>{t("common.price")}</span>
        <strong>{profile.collaborationPrice == null ? t("search.priceNotSpecified") : formatCurrency(profile.collaborationPrice)}</strong>
      </div>
    </a>
    <FavoriteButton brandFaceId={profile.id} className="catalog-brand-face-card__favorite" onChanged={onFavoriteChanged} />
  </article>;
}
