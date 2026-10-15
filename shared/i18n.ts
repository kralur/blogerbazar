// The i18n core shared by the Mini App and the web admin: RU/UZ dictionaries with {param} placeholders
// and Russian plural forms. Each app keeps its own dictionaries and language choice.
export type Language = "ru" | "uz";
export type Dictionary = Record<string, string>;
export type MessageValues = Record<string, string | number>;

// Russian nouns after a number take one of three forms (1 otzyv, 2 otzyva, 5 otzyvov). A text with {count}
// may have "<key>_one" and "<key>_few" variants; the base key holds the "many" form. Uzbek does not inflect.
export function russianPluralForm(count: number): "one" | "few" | "many" {
  const lastTwo = Math.abs(count) % 100;
  const last = lastTwo % 10;
  if (lastTwo >= 11 && lastTwo <= 14) return "many";
  if (last === 1) return "one";
  if (last >= 2 && last <= 4) return "few";
  return "many";
}

// A missing text falls back to Russian, then to the key itself.
export function formatMessage(dictionaries: Record<Language, Dictionary>, language: Language, key: string, values?: MessageValues) {
  const form = language === "ru" && typeof values?.count === "number" ? russianPluralForm(values.count) : "many";
  const pluralKey = form === "many" ? key : `${key}_${form}`;
  const template = dictionaries[language][pluralKey] ?? dictionaries[language][key] ?? dictionaries.ru[key] ?? key;
  return values ? template.replace(/\{(\w+)\}/g, (_, name: string) => String(values[name] ?? `{${name}}`)) : template;
}
