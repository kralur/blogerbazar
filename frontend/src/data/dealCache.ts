import type { DealDetails } from "../api/marketplace";

type Listener = (deal: DealDetails | null) => void;

const byId = new Map<string, DealDetails>();
const listeners = new Set<Listener>();

function notify(deal: DealDetails | null) { listeners.forEach((listener) => listener(deal)); }

export function getCachedDeal(id: string) { return byId.get(id) ?? null; }
export function setCachedDeal(value: DealDetails) {
  byId.set(value.id, value);
  notify(value);
}
export function clearDealCache() {
  byId.clear();
  notify(null);
}
export function subscribeDealCache(listener: Listener) {
  listeners.add(listener);
  return () => { listeners.delete(listener); };
}
