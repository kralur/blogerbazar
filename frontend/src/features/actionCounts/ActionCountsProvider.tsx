import { createContext, useCallback, useContext, useEffect, useRef, useState, type ReactNode } from "react";
import { getMyCampaigns, getMyDeals, getMyOffers, type MarketplaceRole } from "../../api/marketplace";
import { useScreenRefresh } from "../../hooks/useScreenRefresh";

// Things waiting for the user's own action, not "unread": the numbers are always right and disappear once acted on.
export type ActionCounts = { offers: number; applications: number; reviews: number; total: number };

const noActions: ActionCounts = { offers: 0, applications: 0, reviews: 0, total: 0 };
const ActionCountsContext = createContext<ActionCounts>(noActions);
const navigationRefreshMs = 3_000;

export function ActionCountsProvider({ role, enabled, children }: { role?: MarketplaceRole; enabled: boolean; children: ReactNode }) {
  const [counts, setCounts] = useState<ActionCounts>(noActions);
  const requestRef = useRef(0);
  const lastLoadRef = useRef(0);

  const load = useCallback(async () => {
    const requestId = ++requestRef.current;
    lastLoadRef.current = Date.now();
    if (!enabled || (role !== "Blogger" && role !== "Business")) { setCounts(noActions); return; }
    const [deals, roleData] = await Promise.allSettled([getMyDeals(), role === "Business" ? getMyCampaigns({ pageSize: 50 }) : getMyOffers()]);
    if (requestId !== requestRef.current) return;
    const reviews = deals.status === "fulfilled" ? deals.value.filter((deal) => deal.canReview).length : 0;
    let offers = 0;
    let applications = 0;
    if (roleData.status === "fulfilled") {
      if (role === "Business") applications = (roleData.value as Awaited<ReturnType<typeof getMyCampaigns>>).items.reduce((sum, campaign) => sum + (campaign.pendingApplicationsCount ?? 0), 0);
      else offers = (roleData.value as Awaited<ReturnType<typeof getMyOffers>>).filter((offer) => offer.canRespond).length;
    }
    setCounts({ offers, applications, reviews, total: offers + applications + reviews });
  }, [enabled, role]);

  useEffect(() => { void load(); }, [load]);
  useScreenRefresh(load, enabled);
  // Accepting, declining or reviewing ends with navigation, so moving between screens re-checks (at most every 3 s).
  useEffect(() => {
    if (!enabled) return;
    const onNavigate = () => { if (Date.now() - lastLoadRef.current > navigationRefreshMs) void load(); };
    window.addEventListener("hashchange", onNavigate);
    return () => window.removeEventListener("hashchange", onNavigate);
  }, [enabled, load]);

  return <ActionCountsContext.Provider value={counts}>{children}</ActionCountsContext.Provider>;
}

export function useActionCounts() {
  return useContext(ActionCountsContext);
}

export function ActionBadge({ count, label }: { count: number; label: string }) {
  if (count <= 0) return null;
  return <span aria-label={label} className="action-badge" role="status">{count > 9 ? "9+" : count}</span>;
}
