import { useMemo, useState } from "react";
import { useI18n } from "../i18n";
import { maxSpokenLanguages, spokenLanguages, type SpokenLanguage } from "../lib/languages";
import { Chip, Icon, Input } from "./ui";

// Languages are only picked from the list, with search; there is no free text (D49).
export function LanguageMultiSelect({ value, onChange, error, required = false }: { value: SpokenLanguage[]; onChange: (languages: SpokenLanguage[]) => void; error?: string; required?: boolean }) {
  const { t } = useI18n();
  const [query, setQuery] = useState("");
  const available = useMemo(() => {
    const needle = query.trim().toLocaleLowerCase();
    return spokenLanguages.filter((code) => !needle || t(`language.${code}`).toLocaleLowerCase().includes(needle) || code.includes(needle));
  }, [query, t]);
  const toggle = (code: SpokenLanguage) => {
    if (value.includes(code)) onChange(value.filter((item) => item !== code));
    else if (value.length < maxSpokenLanguages) onChange([...value, code]);
  };

  return <section aria-describedby={error ? "language-selector-error" : undefined} aria-label={t("brandFace.languages")} aria-invalid={error ? true : undefined} className="grid gap-3">
    <div className="flex items-center justify-between gap-3"><span className="text-[13px] font-bold text-brand-muted">{t("brandFace.languages")}{required && <span aria-hidden="true" className="ml-1 text-brand-danger">*</span>}</span><span className="text-xs text-brand-muted">{value.length}/{maxSpokenLanguages}</span></div>
    <Input aria-label={t("languageSelect.searchAria")} onChange={(event) => setQuery(event.target.value)} placeholder={t("languageSelect.searchPlaceholder")} value={query} />
    <div className="flex flex-wrap gap-2">
      {available.map((code) => <button aria-pressed={value.includes(code)} className="category-multi-select__choice" disabled={!value.includes(code) && value.length >= maxSpokenLanguages} key={code} onClick={() => toggle(code)} type="button"><Chip active={value.includes(code)}>{t(`language.${code}`)}</Chip></button>)}
      {available.length === 0 && <p className="text-sm text-brand-muted">{t("languageSelect.nothingFound")}</p>}
    </div>
    {value.length > 0 && <div className="flex flex-wrap gap-2">{value.map((code) => <button aria-label={t("languageSelect.remove", { language: t(`language.${code}`) })} className="category-multi-select__selected" key={code} onClick={() => toggle(code)} type="button">{t(`language.${code}`)}<Icon className="h-3.5 w-3.5" name="close" /></button>)}</div>}
    {error && <p className="text-xs font-semibold text-brand-danger" id="language-selector-error">{error}</p>}
  </section>;
}
