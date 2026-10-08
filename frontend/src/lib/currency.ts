// Many Telegram WebViews (Android Chromium) ship without Uzbek locale data and print "2026 M11 1" or "1,500,000".
// Uzbek uses the same digit grouping as Russian (space groups, comma decimals), so numbers are formatted with ru-RU
// and Uzbek month names and compact units come from the dictionary instead of the WebView.
const NUMBER_LOCALE = "ru-RU";
const formatPlain = (value: number, fractionDigits?: number) => new Intl.NumberFormat(NUMBER_LOCALE, fractionDigits == null ? undefined : { maximumFractionDigits: fractionDigits }).format(value);

export const formatRating = (value: number) => formatPlain(value, 1);
export const formatNumber = (value?: number | null) => value == null ? "-" : formatPlain(value);
export const formatCompactNumber = (value?: number | null) => {
  if (value == null) return "-";
  if (currentLanguage() !== "uz") return new Intl.NumberFormat(NUMBER_LOCALE, { notation: "compact", maximumFractionDigits: 1 }).format(value);
  const abs = Math.abs(value);
  if (abs >= 1_000_000_000) return `${formatPlain(value / 1_000_000_000, 1)} ${translate("format.billions", undefined, "uz")}`;
  if (abs >= 1_000_000) return `${formatPlain(value / 1_000_000, 1)} ${translate("format.millions", undefined, "uz")}`;
  if (abs >= 1_000) return `${formatPlain(value / 1_000, 1)} ${translate("format.thousands", undefined, "uz")}`;
  return formatPlain(value);
};
export const formatCurrency = (value?: number | null) => value == null ? translate("card.onRequest") : formatPlain(value) + " " + translate("currency.uzs");
// One budget format for every screen: a localized range, "from …" or "up to …"; null when the budget is open.
export const formatBudgetRange = (min?: number | null, max?: number | null) => {
  const format = (value: number) => formatPlain(value);
  if (min != null && max != null) return min === max ? formatCurrency(min) : translate("campaigns.budgetRange", { min: format(min), max: format(max) });
  if (min != null) return translate("campaigns.budgetFromValue", { min: format(min) });
  if (max != null) return translate("campaigns.budgetToValue", { max: format(max) });
  return null;
};
// Short localized date (day and short month); options add the year or the time.
export const formatShortDate = (value: string | Date, language: Language = currentLanguage(), options: { year?: boolean; time?: boolean } = {}) => {
  const date = new Date(value);
  if (language !== "uz") return new Intl.DateTimeFormat(NUMBER_LOCALE, { day: "numeric", month: "short", ...(options.year ? { year: "numeric" } : {}), ...(options.time ? { hour: "2-digit", minute: "2-digit" } : {}) }).format(date);
  const day = `${date.getDate()}-${translate("format.monthsShort", undefined, "uz").split(",")[date.getMonth()]}`;
  const time = options.time ? `, ${String(date.getHours()).padStart(2, "0")}:${String(date.getMinutes()).padStart(2, "0")}` : "";
  return options.year ? `${day}, ${date.getFullYear()}${time}` : `${day}${time}`;
};
export const formatDate = (value?: string | Date | null) => value ? formatShortDate(value, currentLanguage(), { year: true }) : "-";
// Date inputs speak local calendar days (yyyy-mm-dd).
export const localDay = (offsetDays = 0) => {
  const date = new Date();
  date.setDate(date.getDate() + offsetDays);
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, "0")}-${String(date.getDate()).padStart(2, "0")}`;
};
// A campaign deadline is a calendar day; it stays open through that day (same rule as the backend).
export const isPastDay = (value?: string | null) => Boolean(value) && value!.slice(0, 10) < localDay();
export const normalizeNumericInput = (value: string) => Number(value.replace(/[^\d]/g, "")) || 0;
// Nine digits (up to 999 999 999) cover any real price, budget or audience and stay below the server's limits,
// so a typo or a "1212121212121" test never reaches the API as a number it cannot store.
export const maxNumericDigits = 9;
export const maxFollowers = 500_000_000;
export const formatNumericInput = (value: string) => value.replace(/\D/g, "").replace(/^0+(?=\d)/, "").slice(0, maxNumericDigits).replace(/\B(?=(\d{3})+(?!\d))/g, " ");
export const normalizeDecimalInput = (value: string) => {
  const normalized = value.trim().replace(",", ".").replace(/[^\d.]/g, "");
  const firstSeparator = normalized.indexOf(".");
  const sanitized = firstSeparator < 0 ? normalized : normalized.slice(0, firstSeparator + 1) + normalized.slice(firstSeparator + 1).replace(/\./g, "");
  const parsed = Number(sanitized);
  return Number.isFinite(parsed) ? parsed : 0;
};
export const formatDecimalInput = (value: string | number) => {
  if (String(value).trim() === "") return "";
  return formatPlain(normalizeDecimalInput(String(value)), 2);
};
export const formatPercentage = (value: string | number) => `${formatDecimalInput(value)}%`;
export const formatPhoneInput = (value: string) => {
  const digits = value.replace(/\D/g, "");
  const local = (digits.startsWith("998") ? digits.slice(3) : digits).slice(0, 9);
  const groups = [local.slice(0, 2), local.slice(2, 5), local.slice(5, 7), local.slice(7, 9)].filter(Boolean);
  return groups.length ? `+998 ${groups.join(" ")}` : "+998";
};
import { currentLanguage, translate, type Language } from "../i18n";
