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
  fergana: "fergana",
  // Older data stored the city, not the region.
  urgench: "khorezm",
  nukus: "karakalpakstan",
  karshi: "kashkadarya",
  termez: "surkhandarya",
  gulistan: "sirdarya"
};

export function normalizeRegion(value: string | null | undefined) {
  const normalized = value?.trim().toLocaleLowerCase();
  // An alias maps an older name ("Tashkent", "Nukus") to its region; a region key stays as it is; anything else is "".
  // (The old expression parsed as `(alias ?? isRegion) ? normalized : ""` and returned the raw name.)
  if (!normalized) return "";
  return regionAliases[normalized] ?? ((uzbekistanRegions as readonly string[]).includes(normalized) ? normalized : "");
}
