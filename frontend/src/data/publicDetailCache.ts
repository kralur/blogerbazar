const cacheLifetimeMs = 60_000;
const staleRetentionMs = 5 * 60_000;
const maxEntries = 100;
const entries = new Map<string, { value: unknown; expiresAt: number; discardAt: number }>();

function key(type: "blogger" | "brand-face" | "campaign", id: string) {
  return `${type}:${id}`;
}

export function getCachedPublicDetail<T>(type: "blogger" | "brand-face" | "campaign", id: string) {
  const cacheKey = key(type, id);
  const entry = entries.get(cacheKey);
  if (!entry || entry.discardAt < Date.now()) {
    entries.delete(cacheKey);
    return null;
  }
  return entry.value as T;
}

export function setCachedPublicDetail<T>(type: "blogger" | "brand-face" | "campaign", id: string, value: T) {
  const cacheKey = key(type, id);
  const now = Date.now();
  entries.delete(cacheKey);
  const oldestKey = entries.keys().next().value;
  if (entries.size >= maxEntries && oldestKey) entries.delete(oldestKey);
  entries.set(cacheKey, { value, expiresAt: now + cacheLifetimeMs, discardAt: now + staleRetentionMs });
}

export function removeCachedPublicDetail(type: "blogger" | "brand-face" | "campaign", id: string) {
  entries.delete(key(type, id));
}

export function clearPublicDetailCache() {
  entries.clear();
}
