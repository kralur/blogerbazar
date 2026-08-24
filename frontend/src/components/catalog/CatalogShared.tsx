import { type ReactNode } from "react";
import { useI18n } from "../../i18n";
import { useTelegram } from "../../telegram/TelegramProvider";
import { Icon, Skeleton } from "../ui";

export function ActiveFilterChips<Key extends string>({ chips, onRemove, onReset, showReset = true }: { chips: Array<{ key: Key; label: string }>; onRemove: (key: Key) => void; onReset: () => void; showReset?: boolean }) {
  const { t } = useI18n();
  if (!chips.length) return null;
  return <div aria-label={t("search.activeFilters")} className="catalog-search__chips" role="group">
    {chips.map((chip) => <button aria-label={t("search.removeFilterAria", { filter: chip.label })} className="catalog-search__chip" key={chip.key} onClick={() => onRemove(chip.key)} type="button"><span>{chip.label}</span><Icon aria-hidden="true" className="h-3.5 w-3.5" name="close" /></button>)}
    {showReset && <button className="catalog-search__reset-all" onClick={onReset} type="button">{t("search.resetAll")}</button>}
  </div>;
}

export function FilterSelect({ label, value, options, onChange }: { label: string; value: string; options: string[][]; onChange: (value: string) => void }) {
  const { haptic } = useTelegram();
  return <label className="catalog-search__filter-select"><span>{label}</span><select onChange={(event) => { haptic.selection(); onChange(event.target.value); }} value={value}>{options.map(([optionValue, optionLabel]) => <option key={optionValue} value={optionValue}>{optionLabel}</option>)}</select></label>;
}

export function SearchSkeleton({ count, compact = false }: { count: number; compact?: boolean }) {
  return <div aria-hidden="true" className={compact ? "catalog-search__next-skeletons" : "catalog-search__skeletons"}>{Array.from({ length: count }, (_, index) => <Skeleton className={compact ? "catalog-search__skeleton catalog-search__skeleton--compact" : "catalog-search__skeleton"} key={index} />)}</div>;
}

export function CatalogState({ title, subtitle, icon, onRetry, compact = false }: { title: string; subtitle: string; icon: string; onRetry?: () => void; compact?: boolean }) {
  const { t } = useI18n();
  return <div className={`catalog-search__state${compact ? " catalog-search__state--compact" : ""}`} role="status"><span aria-hidden="true" className="catalog-search__state-icon"><Icon name={icon} /></span><h2>{title}</h2><p>{subtitle}</p>{onRetry && <button className="catalog-search__primary-button" onClick={onRetry} type="button">{t("common.retry")}</button>}</div>;
}

export function CatalogHeader({ children }: { children: ReactNode }) {
  return <header className="catalog-search__header">{children}</header>;
}
