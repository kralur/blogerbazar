function locale() {
  return localStorage.getItem("bloggerbazar.language") === "uz" ? "uz-UZ" : "ru-UZ";
}

export const formatNumber = (value?: number | null) => value == null ? "—" : new Intl.NumberFormat(locale()).format(value);
export const formatCompactNumber = (value?: number | null) => value == null ? "—" : new Intl.NumberFormat(locale(), { notation: "compact", maximumFractionDigits: 1 }).format(value);
export const formatCurrency = (value?: number | null) => value == null ? translate("card.onRequest") : new Intl.NumberFormat(currentLanguage() === "uz" ? "uz-UZ" : "ru-RU").format(value) + " " + translate("currency.uzs");
export const formatDate = (value?: string | Date | null) => value ? new Intl.DateTimeFormat(locale(), { day: "numeric", month: "short", year: "numeric" }).format(new Date(value)) : "—";
export const normalizeNumericInput = (value: string) => Number(value.replace(/[^\d]/g, "")) || 0;
export const formatNumericInput = (value: string) => value.replace(/\D/g, "").replace(/\B(?=(\d{3})+(?!\d))/g, " ");
export const formatPhoneInput = (value: string) => {
  const digits = value.replace(/\D/g, "").replace(/^8/, "998").slice(0, 12);
  const local = digits.startsWith("998") ? digits.slice(3) : digits;
  const groups = [local.slice(0, 2), local.slice(2, 5), local.slice(5, 7), local.slice(7, 9)].filter(Boolean);
  return groups.length ? `+998 ${groups.join(" ")}` : "";
};
import { currentLanguage, translate } from "../i18n";
