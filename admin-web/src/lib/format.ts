import type { Language } from "../i18n";

// Numbers, money (sums) and Tashkent dates for the screen; the API sends plain numbers and ISO dates.
const numbers = new Intl.NumberFormat("ru-RU");

export const formatNumber = (value: number) => numbers.format(value);

// Axis labels for big sums: "3,5 mln" in Uzbek, the Russian short form in Russian.
export const formatCompact = (value: number, language: Language) =>
  new Intl.NumberFormat(language === "uz" ? "uz-Latn" : "ru-RU", { notation: "compact", maximumFractionDigits: 1 }).format(value);

// "2026-10-15" -> "15.10"
export const formatDay = (date: string) => `${date.slice(8, 10)}.${date.slice(5, 7)}`;

// "2026-10-15" -> "15.10.2026"
export const formatDate = (date: string) => `${formatDay(date)}.${date.slice(0, 4)}`;

export const percent = (part: number, whole: number) => (whole > 0 ? `${Math.round((part * 100) / whole)}%` : null);

type Translate = (key: string, values?: Record<string, string | number>) => string;

export const formatMoney = (value: number, t: Translate) => t("common.money", { amount: formatNumber(value) });

// "2026-10" -> the short month name and the year, in the chosen language.
export const formatMonth = (month: string, t: Translate) => `${t(`month.short.${Number(month.slice(5, 7))}`)} ${month.slice(0, 4)}`;

// City and category keys as stored on profiles; a custom category is "other:<text>", an unknown key shows as is.
export function taxonomyLabel(kind: "city" | "category", key: string, t: Translate) {
  if (key.startsWith("other:")) return key.slice("other:".length).trim() || key;
  const name = `taxonomy.${kind}.${key.toLowerCase()}`;
  const label = t(name);
  return label === name ? key : label;
}

// Today's Tashkent date as "yyyy-MM-dd", shifted by a number of days.
export function tashkentDate(offsetDays = 0, now = new Date()) {
  const tashkent = new Date(now.getTime() + 5 * 3600_000 + offsetDays * 86_400_000);
  return tashkent.toISOString().slice(0, 10);
}

// Whole days from one "yyyy-MM-dd" to another, both included.
export const daysInRange = (from: string, to: string) => Math.round((Date.parse(`${to}T00:00:00Z`) - Date.parse(`${from}T00:00:00Z`)) / 86_400_000) + 1;
