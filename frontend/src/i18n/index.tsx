import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from "react";
import { formatMessage, russianPluralForm, type Dictionary, type Language, type MessageValues as Values } from "@bloggerbazar/shared/i18n";
import taxonomyRu from "@bloggerbazar/shared/taxonomy/ru.json";
import taxonomyUz from "@bloggerbazar/shared/taxonomy/uz.json";
import funnelRu from "@bloggerbazar/shared/funnel/ru.json";
import funnelUz from "@bloggerbazar/shared/funnel/uz.json";
import ru from "./ru.json";
import uz from "./uz.json";

export type { Language };
export { russianPluralForm };
// City and category names and the funnel texts are shared with the web admin (shared/taxonomy, shared/funnel).
const dictionaries: Record<Language, Dictionary> = { ru: { ...taxonomyRu, ...funnelRu, ...ru }, uz: { ...taxonomyUz, ...funnelUz, ...uz } };
const storageKey = "bloggerbazar.language";

function toSupportedLanguage(value?: string | null): Language | undefined {
  return value === "ru" || value === "uz" ? value : undefined;
}

function storedLanguage(): Language | undefined {
  try {
    return toSupportedLanguage(localStorage.getItem(storageKey));
  } catch {
    return undefined;
  }
}

function telegramLanguage(): Language | undefined {
  if (typeof window === "undefined") return undefined;
  const telegram = window.Telegram?.WebApp?.initDataUnsafe?.user as { language_code?: string } | undefined;
  return toSupportedLanguage(telegram?.language_code?.toLowerCase());
}

export function currentLanguage(): Language {
  return storedLanguage() ?? telegramLanguage() ?? "ru";
}

// Plural forms and {param} placeholders: the shared i18n core (shared/i18n.ts).
export function translate(key: string, values?: Values, language = currentLanguage()) {
  return formatMessage(dictionaries, language, key, values);
}

const categoryAliases: Record<string, string> = {
  lifestyle: "lifestyle", beauty: "beauty", "красота": "beauty", food: "food", "еда": "food", technology: "technology", tech: "technology", "технологии": "technology", sport: "sport", "спорт": "sport", travel: "travel", "путешествия": "travel", finance: "finance", "финансы": "finance", gaming: "gaming", "игры": "gaming", fashion: "fashion", "мода": "fashion"
};
const cityAliases: Record<string, string> = {
  tashkent: "tashkent", "ташкент": "tashkent", samarkand: "samarkand", "самарканд": "samarkand", bukhara: "bukhara", "бухара": "bukhara", fergana: "fergana", "фергана": "fergana", andijan: "andijan", "андижан": "andijan", namangan: "namangan", "наманган": "namangan", uzbekistan: "uzbekistan", "узбекистан": "uzbekistan"
};
// "other:<text>" is a category the user typed in; an unknown key never reaches the screen as a raw key.
export const categoryLabel = (value: string, language = currentLanguage()) => {
  if (value.startsWith("other:")) {
    // Older forms saved the placeholder "other:other" when the field was left empty: show it as "Other".
    const text = value.slice("other:".length).trim();
    return !text || text.toLowerCase() === "other" ? translate("categorySelect.other", undefined, language) : text;
  }
  const key = `taxonomy.category.${categoryAliases[value.toLowerCase()] ?? value.toLowerCase()}`;
  const label = translate(key, undefined, language);
  return label === key ? translate("common.notSpecified", undefined, language) : label;
};
// An unknown city (older data or a direct API call) never reaches the screen as a raw key.
export const cityLabel = (value: string, language = currentLanguage()) => {
  const key = `taxonomy.city.${cityAliases[value.toLowerCase()] ?? value.toLowerCase()}`;
  const label = translate(key, undefined, language);
  return label === key ? translate("common.notSpecified", undefined, language) : label;
};

type I18nContextValue = { language: Language; setLanguage: (language: Language) => void; t: (key: string, values?: Values) => string };
const I18nContext = createContext<I18nContextValue | null>(null);

export function I18nProvider({ children }: { children: ReactNode }) {
  const [language, setLanguageState] = useState<Language>(currentLanguage);
  useEffect(() => {
    document.documentElement.lang = language;
  }, [language]);
  const value = useMemo<I18nContextValue>(() => ({
    language,
    setLanguage: (nextLanguage) => {
      try { localStorage.setItem(storageKey, nextLanguage); } catch {}
      setLanguageState(nextLanguage);
    },
    t: (key, values) => translate(key, values, language)
  }), [language]);
  return <I18nContext.Provider value={value}>{children}</I18nContext.Provider>;
}

export function useI18n() {
  const context = useContext(I18nContext);
  if (!context) throw new Error("useI18n must be used inside I18nProvider.");
  return context;
}
