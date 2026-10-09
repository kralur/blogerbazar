import { translate } from "../i18n";

// Spoken languages a profile can list (D49): fixed ISO codes, picked from a list, never typed,
// so different spellings of one language do not become different languages. Labels come from i18n.
export const spokenLanguages = ["uz", "ru", "en", "kaa", "tg", "kk", "ky", "tk", "tr", "fa", "ar", "zh", "ko", "ja", "de", "fr", "es", "it", "hi"] as const;
export type SpokenLanguage = typeof spokenLanguages[number];
export const maxSpokenLanguages = 5;

// Latin-script aliases; Russian and Uzbek names come from the i18n dictionaries (one source of truth).
const latinAliases: Partial<Record<SpokenLanguage, string[]>> = {
  uz: ["uzb", "uzbek", "o'zbekcha", "ozbekcha"], ru: ["rus", "russian"], en: ["eng", "english"], tr: ["turkish"],
  ar: ["arabic"], zh: ["chinese"], ko: ["korean"], ja: ["japanese"], de: ["german"], fr: ["french"],
  es: ["spanish"], it: ["italian"], hi: ["hindi"]
};

export function isSpokenLanguage(value: string): value is SpokenLanguage {
  return (spokenLanguages as readonly string[]).includes(value);
}

// Profiles saved before D49 hold free text; known spellings map to a code, the rest is dropped on the next save.
export function normalizeSpokenLanguages(values: string[]): SpokenLanguage[] {
  const result: SpokenLanguage[] = [];
  for (const value of values) {
    const needle = value.trim().toLocaleLowerCase();
    const code = spokenLanguages.find((candidate) => candidate === needle
      || latinAliases[candidate]?.includes(needle)
      || (["ru", "uz"] as const).some((language) => translate(`language.${candidate}`, undefined, language).toLocaleLowerCase() === needle));
    if (code && !result.includes(code)) result.push(code);
  }
  return result.slice(0, maxSpokenLanguages);
}

export function spokenLanguageLabelKey(code: string) {
  return isSpokenLanguage(code) ? `language.${code}` : null;
}
