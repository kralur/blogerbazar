import { createContext, useContext, useMemo, useState, type ReactNode } from "react";
import ru from "./ru.json";
import uz from "./uz.json";

export type Language = "ru" | "uz";
type Dictionary = Record<string, string>;
type Values = Record<string, string | number>;
const dictionaries: Record<Language, Dictionary> = { ru, uz };
const storageKey = "bloggerbazar.language";

export function currentLanguage(): Language {
  return localStorage.getItem(storageKey) === "uz" ? "uz" : "ru";
}

export function translate(key: string, values?: Values, language = currentLanguage()) {
  const template = dictionaries[language][key] ?? dictionaries.ru[key] ?? key;
  return values ? template.replace(/\{(\w+)\}/g, (_, name) => String(values[name] ?? `{${name}}`)) : template;
}

const categoryAliases: Record<string, string> = {
  lifestyle: "lifestyle", beauty: "beauty", "красота": "beauty", food: "food", "еда": "food", technology: "technology", tech: "technology", "технологии": "technology", sport: "sport", "спорт": "sport", travel: "travel", "путешествия": "travel", finance: "finance", "финансы": "finance", gaming: "gaming", "игры": "gaming", fashion: "fashion", "мода": "fashion"
};
const cityAliases: Record<string, string> = {
  tashkent: "tashkent", "ташкент": "tashkent", samarkand: "samarkand", "самарканд": "samarkand", bukhara: "bukhara", "бухара": "bukhara", fergana: "fergana", "фергана": "fergana", andijan: "andijan", "андижан": "andijan", namangan: "namangan", "наманган": "namangan", uzbekistan: "uzbekistan", "узбекистан": "uzbekistan"
};
export const categoryLabel = (value: string, language = currentLanguage()) => translate(`taxonomy.category.${categoryAliases[value.toLowerCase()] ?? value.toLowerCase()}`, undefined, language);
export const cityLabel = (value: string, language = currentLanguage()) => translate(`taxonomy.city.${cityAliases[value.toLowerCase()] ?? value.toLowerCase()}`, undefined, language);

type I18nContextValue = { language: Language; setLanguage: (language: Language) => void; t: (key: string, values?: Values) => string };
const I18nContext = createContext<I18nContextValue | null>(null);

export function I18nProvider({ children }: { children: ReactNode }) {
  const [language, setLanguageState] = useState<Language>(currentLanguage);
  const value = useMemo<I18nContextValue>(() => ({
    language,
    setLanguage: (nextLanguage) => { localStorage.setItem(storageKey, nextLanguage); setLanguageState(nextLanguage); },
    t: (key, values) => translate(key, values, language)
  }), [language]);
  return <I18nContext.Provider value={value}>{children}</I18nContext.Provider>;
}

export function useI18n() {
  const context = useContext(I18nContext);
  if (!context) throw new Error("useI18n must be used inside I18nProvider.");
  return context;
}
