import { useState, type FormEvent } from "react";
import type { Period } from "../api/analytics";
import { useI18n } from "../i18n";
import { daysInRange, formatDate, tashkentDate } from "../lib/format";
import { Alert, Button, Choice } from "./ui";

// Last 7, 30 or 90 Tashkent days, or any range of up to a year that does not end in the future.
export type PeriodChoice = { preset: "7" | "30" | "90" | "custom"; from: string; to: string };
export const maxRangeDays = 366;

export function presetPeriod(days: 7 | 30 | 90, now = new Date()): PeriodChoice {
  return { preset: String(days) as PeriodChoice["preset"], from: tashkentDate(-(days - 1), now), to: tashkentDate(0, now) };
}

export const asPeriod = ({ from, to }: PeriodChoice): Period => ({ from, to });

export function PeriodPicker({ value, onChange }: { value: PeriodChoice; onChange: (value: PeriodChoice) => void }) {
  const { t } = useI18n();
  const [custom, setCustom] = useState(value.preset === "custom");
  const [from, setFrom] = useState(value.from);
  const [to, setTo] = useState(value.to);
  const [error, setError] = useState<string | null>(null);
  const today = tashkentDate();

  const choose = (preset: PeriodChoice["preset"]) => {
    setError(null);
    if (preset === "custom") {
      setCustom(true);
      return;
    }
    setCustom(false);
    onChange(presetPeriod(Number(preset) as 7 | 30 | 90));
  };

  const apply = (event: FormEvent) => {
    event.preventDefault();
    if (!from || !to || from > to) setError(t("period.errorOrder"));
    else if (to > today) setError(t("period.errorFuture"));
    else if (daysInRange(from, to) > maxRangeDays) setError(t("period.errorLong"));
    else {
      setError(null);
      onChange({ preset: "custom", from, to });
    }
  };

  const inputClass = "h-10 min-w-0 rounded-[var(--bb-radius-control)] border border-brand-line bg-brand-surface px-3 text-sm text-brand-ink";
  return (
    <div className="space-y-3">
      <Choice
        label={t("period.label")}
        value={custom ? "custom" : value.preset}
        onChange={choose}
        options={[
          { value: "7", label: t("period.days", { count: 7 }) },
          { value: "30", label: t("period.days", { count: 30 }) },
          { value: "90", label: t("period.days", { count: 90 }) },
          { value: "custom", label: t("period.custom") }
        ]}
      />
      {custom && (
        <form className="flex flex-wrap items-end gap-3" onSubmit={apply} noValidate>
          <label className="grid gap-1 text-xs text-brand-muted">
            {t("period.from")}
            <input type="date" className={inputClass} value={from} max={today} onChange={(event) => setFrom(event.target.value)} />
          </label>
          <label className="grid gap-1 text-xs text-brand-muted">
            {t("period.to")}
            <input type="date" className={inputClass} value={to} max={today} onChange={(event) => setTo(event.target.value)} />
          </label>
          <Button type="submit" variant="primary">{t("period.apply")}</Button>
        </form>
      )}
      {error && <Alert>{error}</Alert>}
      <p className="text-xs text-brand-muted">{t("period.shown", { from: formatDate(value.from), to: formatDate(value.to) })}</p>
    </div>
  );
}
