const platformKeys: Record<string, string> = {
  instagram: "search.platformInstagram",
  telegram: "search.platformTelegram",
  tiktok: "search.platformTiktok",
  youtube: "search.platformYoutube"
};

// Platform names come from the API in lowercase ("instagram"); show the localized brand name instead.
export function platformLabel(type: string | null | undefined, t: (key: string) => string) {
  if (!type) return "";
  const key = platformKeys[type.toLowerCase()];
  return key ? t(key) : type;
}
