import type { ChangeEventHandler } from "react";
import { cityLabel, useI18n } from "../i18n";
import { uzbekistanRegions } from "../lib/taxonomy";

export function RegionSelect({ value, onChange, required = false, label, error, includeAny = false, className }: { value: string; onChange: ChangeEventHandler<HTMLSelectElement>; required?: boolean; label?: string; error?: string; includeAny?: boolean; className?: string }) {
  const { t } = useI18n();
  return <label className={`grid gap-2 ${className ?? ""}`}><span className="text-[13px] font-bold text-brand-muted">{label ?? t("common.city")}{required && <span aria-hidden="true" className="ml-1 text-brand-danger">*</span>}</span><select aria-invalid={error ? true : undefined} className={`h-[52px] rounded-2xl border bg-brand-surface px-4 text-[15px] text-brand-ink outline-none transition focus:border-brand-accent ${error ? "border-brand-danger focus:border-brand-danger" : "border-brand-line"}`} onChange={onChange} value={value}><option value="">{includeAny ? t("common.any") : t("regionSelect.placeholder")}</option>{uzbekistanRegions.map((region) => <option key={region} value={region}>{cityLabel(region)}</option>)}</select>{error && <span className="text-xs font-semibold text-brand-danger">{error}</span>}</label>;
}
