import { useI18n } from "../../i18n";

export type CatalogType = "blogger" | "brand-face";

export function CatalogTypeSegmentedControl({ value, onChange }: { value: CatalogType; onChange: (type: CatalogType) => void }) {
  const { t } = useI18n();
  const options: Array<{ type: CatalogType; label: string }> = [
    { type: "blogger", label: t("search.typeBloggers") },
    { type: "brand-face", label: t("search.typeBrandFaces") }
  ];

  return <div aria-label={t("search.creatorType")} className="catalog-search__segments" role="group">
    {options.map((option) => {
      const selected = value === option.type;
      return <button aria-pressed={selected} className={`catalog-search__segment${selected ? " catalog-search__segment--selected" : ""}`} key={option.type} onClick={() => onChange(option.type)} type="button">
        <span>{option.label}</span>{selected && <span aria-hidden="true" className="catalog-search__segment-indicator">✓</span>}
      </button>;
    })}
  </div>;
}
