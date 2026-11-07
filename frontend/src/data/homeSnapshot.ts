import type { getMarketplaceHome, MarketplaceRole } from "../api/marketplace";

// The answer as the app uses it (getMarketplaceHome shapes the raw API answer).
type MarketplaceHome = Awaited<ReturnType<typeof getMarketplaceHome>>;

// The last Home answer per role, kept on the phone so the next app open draws Home at once and refreshes
// in the background. Only the public catalog answer is kept (no contacts, no personal tasks).
// AccountActions removes every "bloggerbazar." key on logout; clearHomeSnapshot covers the rest.
const keyPrefix = "bloggerbazar.homeSnapshot.";
export const homeSnapshotMaxAgeMs = 7 * 24 * 60 * 60 * 1000;
const maxBytes = 200_000;

type Stored = { savedAt: number; data: MarketplaceHome };

export function readHomeSnapshot(role: MarketplaceRole): MarketplaceHome | null {
  try {
    const raw = localStorage.getItem(keyPrefix + role);
    if (!raw) return null;
    const stored = JSON.parse(raw) as Partial<Stored>;
    if (!stored || typeof stored.savedAt !== "number" || !stored.data || Date.now() - stored.savedAt > homeSnapshotMaxAgeMs) return null;
    return stored.data;
  } catch {
    return null;
  }
}

export function writeHomeSnapshot(role: MarketplaceRole, data: MarketplaceHome) {
  try {
    const raw = JSON.stringify({ savedAt: Date.now(), data } satisfies Stored);
    if (raw.length > maxBytes) return;
    localStorage.setItem(keyPrefix + role, raw);
  } catch {
    // A full or blocked storage only means the next start loads Home as before.
  }
}

export function clearHomeSnapshot() {
  try {
    for (let index = localStorage.length - 1; index >= 0; index -= 1) {
      const key = localStorage.key(index);
      if (key?.startsWith(keyPrefix)) localStorage.removeItem(key);
    }
  } catch {}
}
