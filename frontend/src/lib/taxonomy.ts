export const uzbekistanRegions = [
  "karakalpakstan",
  "tashkent-city",
  "tashkent-region",
  "andijan",
  "bukhara",
  "jizzakh",
  "kashkadarya",
  "navoi",
  "namangan",
  "samarkand",
  "sirdarya",
  "surkhandarya",
  "fergana",
  "khorezm"
] as const;

export const marketplaceCategories = [
  "lifestyle",
  "beauty",
  "food",
  "technology",
  "sport",
  "travel",
  "finance",
  "gaming",
  "fashion"
] as const;

export const otherCategoryPrefix = "other:";

export function isOtherCategory(value: string) {
  return value.startsWith(otherCategoryPrefix);
}

const regionAliases: Record<string, typeof uzbekistanRegions[number]> = {
  tashkent: "tashkent-city",
  "tashkent city": "tashkent-city",
  "tashkent region": "tashkent-region",
  samarkand: "samarkand",
  bukhara: "bukhara",
  andijan: "andijan",
  namangan: "namangan",
  fergana: "fergana"
};

export function normalizeRegion(value: string | null | undefined) {
  const normalized = value?.trim().toLocaleLowerCase();
  return normalized && (regionAliases[normalized] ?? (uzbekistanRegions as readonly string[]).includes(normalized) ? normalized : "") || "";
}
