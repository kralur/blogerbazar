import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from "react";
import { formatMessage, type Dictionary, type Language, type MessageValues } from "@bloggerbazar/shared/i18n";
import ru from "./ru.json";
import uz from "./uz.json";
import { toNewUzbekDictionary } from "@bloggerbazar/shared/uzbekScript";

export type { Language };
// Uzbek in the new Latin alphabet (law of 21 October 2026), derived from the texts written in the old one.
export const dictionaries: Record<Language, Dictionary> = { ru, uz: toNewUzbekDictionary(uz) };
const storageKey = "bloggerbazar.landing.language";

// The saved choice, then the browser language; Russian otherwise.
export function initialLanguage(): Language {
  try {
    const saved = localStorage.getItem(storageKey);
    if (saved === "ru" || saved === "uz") return saved;
  } catch {}
  return typeof navigator !== "undefined" && navigator.language?.toLowerCase().startsWith("uz") ? "uz" : "ru";
}

type I18nContextValue = { language: Language; setLanguage: (language: Language) => void; t: (key: string, values?: MessageValues) => string };
const I18nContext = createContext<I18nContextValue | null>(null);

export function I18nProvider({ children }: { children: ReactNode }) {
  const [language, setLanguageState] = useState<Language>(initialLanguage);
  useEffect(() => {
    document.documentElement.lang = language;
  }, [language]);
  const value = useMemo<I18nContextValue>(() => ({
    language,
    setLanguage: (next) => {
      try { localStorage.setItem(storageKey, next); } catch {}
      setLanguageState(next);
    },
    t: (key, values) => formatMessage(dictionaries, language, key, values)
  }), [language]);
  return <I18nContext.Provider value={value}>{children}</I18nContext.Provider>;
}

export function useI18n() {
  const context = useContext(I18nContext);
  if (!context) throw new Error("useI18n must be used inside I18nProvider.");
  return context;
}
