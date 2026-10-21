import { useEffect, useState } from "react";

// D58: three totals from /api/public/stats (the site's nginx asks the API and keeps the answer ten minutes).
export type PublicStats = { bloggers: number; businesses: number; completedDeals: number };

const isCount = (value: unknown): value is number => typeof value === "number" && Number.isSafeInteger(value) && value >= 0;

export function parseStats(value: unknown): PublicStats | null {
  if (!value || typeof value !== "object") return null;
  const { bloggers, businesses, completedDeals } = value as Record<string, unknown>;
  return isCount(bloggers) && isCount(businesses) && isCount(completedDeals) ? { bloggers, businesses, completedDeals } : null;
}

// Null while loading and when the numbers are not available: the page then simply has no numbers block.
export function usePublicStats() {
  const [stats, setStats] = useState<PublicStats | null>(null);
  useEffect(() => {
    const controller = new AbortController();
    fetch("/api/public/stats", { signal: controller.signal, headers: { Accept: "application/json" } })
      .then((response) => (response.ok ? response.json() : null))
      .then((body: unknown) => setStats(parseStats(body)))
      .catch(() => {});
    return () => controller.abort();
  }, []);
  return stats;
}
