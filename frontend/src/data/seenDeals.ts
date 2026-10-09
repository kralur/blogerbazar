// Which deals this person has already opened on this device, so a deal created by the partner
// (an accepted offer or application) shows a red dot until it is opened once.
const storageKey = "bloggerbazar.seen-deals.v1";
const maxRemembered = 300;

type SeenDeals = { ids: string[] };

function read(): SeenDeals | null {
  try {
    const raw = localStorage.getItem(storageKey);
    if (!raw) return null;
    const parsed = JSON.parse(raw) as SeenDeals;
    return Array.isArray(parsed.ids) ? parsed : null;
  } catch {
    return null;
  }
}

function write(value: SeenDeals) {
  try {
    localStorage.setItem(storageKey, JSON.stringify({ ids: value.ids.slice(-maxRemembered) }));
  } catch {}
}

// The first look on a device marks every existing deal as seen: only deals that appear later are new.
export function unseenActiveDealIds(deals: { id: string; status: number }[]) {
  const seen = read();
  if (!seen) {
    write({ ids: deals.map((deal) => deal.id) });
    return new Set<string>();
  }
  const known = new Set(seen.ids);
  return new Set(deals.filter((deal) => deal.status === 0 && !known.has(deal.id)).map((deal) => deal.id));
}

export function markDealSeen(id: string) {
  const seen = read() ?? { ids: [] };
  if (seen.ids.includes(id)) return;
  write({ ids: [...seen.ids, id] });
  window.dispatchEvent(new Event(seenDealsChangedEvent));
}

export const seenDealsChangedEvent = "bloggerbazar:seen-deals-changed";
