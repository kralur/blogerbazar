import { useMemo, useState } from "react";
import { categoryLabel, useI18n } from "../i18n";
import { isOtherCategory, marketplaceCategories, otherCategoryPrefix } from "../lib/taxonomy";
import { Chip, Icon, Input } from "./ui";

const maxCategories = 5;

export function CategoryMultiSelect({ value, onChange, error, required = false }: { value: string[]; onChange: (categories: string[]) => void; error?: string; required?: boolean }) {
  const { t } = useI18n();
  const [query, setQuery] = useState("");
  const [other, setOther] = useState(() => value.find(isOtherCategory)?.slice(otherCategoryPrefix.length) ?? "");
  const available = useMemo(() => marketplaceCategories.filter((category) => categoryLabel(category).toLocaleLowerCase().includes(query.trim().toLocaleLowerCase())), [query, t]);
  const selectedOther = value.find(isOtherCategory);
  const toggle = (category: string) => {
    if (value.includes(category)) onChange(value.filter((item) => item !== category));
    else if (value.length < maxCategories) onChange([...value, category]);
  };
  const updateOther = (nextValue: string) => {
    setOther(nextValue);
    const withoutOther = value.filter((item) => !isOtherCategory(item));
    onChange(nextValue.trim() ? [...withoutOther, `${otherCategoryPrefix}${nextValue.trim()}`] : withoutOther);
  };
  const toggleOther = () => {
    if (selectedOther) {
      setOther("");
      onChange(value.filter((item) => !isOtherCategory(item)));
    } else if (value.length < maxCategories) onChange([...value, `${otherCategoryPrefix}${other.trim() || "other"}`]);
  };

  return <section aria-describedby={error ? "category-selector-error" : undefined} aria-invalid={error ? true : undefined} className="grid gap-3">
    <div className="flex items-center justify-between gap-3"><span className="text-[13px] font-bold text-brand-muted">{t("common.categories")}{required && <span aria-hidden="true" className="ml-1 text-brand-danger">*</span>}</span><span className="text-xs text-brand-muted">{value.length}/{maxCategories}</span></div>
    <Input aria-label={t("categorySelect.searchAria")} onChange={(event) => setQuery(event.target.value)} placeholder={t("categorySelect.searchPlaceholder")} value={query} />
    <div className="flex flex-wrap gap-2">
      {available.map((category) => <button aria-pressed={value.includes(category)} key={category} onClick={() => toggle(category)} type="button"><Chip active={value.includes(category)}>{categoryLabel(category)}</Chip></button>)}
      <button aria-pressed={Boolean(selectedOther)} onClick={toggleOther} type="button"><Chip active={Boolean(selectedOther)}>{t("categorySelect.other")}</Chip></button>
    </div>
    {selectedOther && <Input label={t("categorySelect.otherLabel")} maxLength={44} onChange={(event) => updateOther(event.target.value)} placeholder={t("categorySelect.otherPlaceholder")} value={other} />}
    {value.length > 0 && <div className="flex flex-wrap gap-2">{value.map((category) => <button aria-label={t("categorySelect.remove", { category: isOtherCategory(category) ? category.slice(otherCategoryPrefix.length) : categoryLabel(category) })} className="inline-flex items-center gap-1 rounded-full bg-blue-50 px-3 py-1.5 text-xs font-bold text-brand-blue" key={category} onClick={() => toggle(category)} type="button">{isOtherCategory(category) ? category.slice(otherCategoryPrefix.length) : categoryLabel(category)}<Icon className="h-3.5 w-3.5" name="close" /></button>)}</div>}
    {error && <p className="text-xs font-semibold text-brand-danger" id="category-selector-error">{error}</p>}
  </section>;
}
