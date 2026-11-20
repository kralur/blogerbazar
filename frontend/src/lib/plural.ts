type Translate = (key: string, values?: Record<string, string | number>) => string;

// Russian number forms: one (1, 21), few (2-4, 22-24), many (5-20, 11-14). Uzbek has one form.
export function pluralKey(count: number, language: string, base: string) {
  if (language !== "ru") return base;
  const remainder = count % 100;
  if (remainder >= 11 && remainder <= 14) return `${base}Many`;
  const lastDigit = count % 10;
  if (lastDigit === 1) return `${base}One`;
  if (lastDigit >= 2 && lastDigit <= 4) return `${base}Few`;
  return `${base}Many`;
}

export const reviewsLabel = (count: number, language: string, t: Translate) => t(pluralKey(count, language, "common.reviews"), { count });
