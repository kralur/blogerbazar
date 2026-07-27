import { createContext, useContext, useMemo, useState, type ReactNode } from "react";
import ru from "./ru.json";
import uz from "./uz.json";

export type Language = "ru" | "uz";
type Dictionary = Record<keyof typeof ru, string>;
const dictionaries: Record<Language, Dictionary> = { ru, uz };
const storageKey = "bloggerbazar.language";

type I18nContextValue = { language: Language; setLanguage: (language: Language) => void; t: (key: keyof typeof ru) => string };
const I18nContext = createContext<I18nContextValue | null>(null);

export function I18nProvider({ children }: { children: ReactNode }) {
  const [language, setLanguageState] = useState<Language>(() => localStorage.getItem(storageKey) === "uz" ? "uz" : "ru");
  const value = useMemo<I18nContextValue>(() => ({
    language,
    setLanguage: (nextLanguage) => { localStorage.setItem(storageKey, nextLanguage); setLanguageState(nextLanguage); },
    t: (key) => dictionaries[language][key]
  }), [language]);
  return <I18nContext.Provider value={value}>{children}</I18nContext.Provider>;
}

export function useI18n() {
  const context = useContext(I18nContext);
  if (!context) throw new Error("useI18n must be used inside I18nProvider.");
  return context;
}
