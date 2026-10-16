import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from "react";
import { formatMessage, type Dictionary, type Language, type MessageValues } from "@bloggerbazar/shared/i18n";
import adminRu from "@bloggerbazar/shared/admin/ru.json";
import adminUz from "@bloggerbazar/shared/admin/uz.json";
import funnelRu from "@bloggerbazar/shared/funnel/ru.json";
import funnelUz from "@bloggerbazar/shared/funnel/uz.json";
import taxonomyRu from "@bloggerbazar/shared/taxonomy/ru.json";
import taxonomyUz from "@bloggerbazar/shared/taxonomy/uz.json";
import ru from "./ru.json";
import uz from "./uz.json";

export type { Language };
// City and category names, the funnel texts and the admin labels come from shared/, the same words as in the Mini App.
export const dictionaries: Record<Language, Dictionary> = { ru: { ...taxonomyRu, ...funnelRu, ...adminRu, ...ru }, uz: { ...taxonomyUz, ...funnelUz, ...adminUz, ...uz } };
const storageKey = "bloggerbazar.admin.language";

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
