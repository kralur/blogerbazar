import type { CampaignApplicationStatus } from "../lib/campaignApplicationStatus";
import type { MyCampaignApplicationDetails } from "../api/marketplace";

type Summary = { id: string; status: CampaignApplicationStatus };
type Listener = () => void;

const byCampaign = new Map<string, Summary>();
const byId = new Map<string, MyCampaignApplicationDetails>();
const listeners = new Set<Listener>();

function notify() { listeners.forEach((listener) => listener()); }

export function getCachedCampaignApplication(campaignId: string) { return byCampaign.get(campaignId) ?? null; }
export function setCachedCampaignApplication(campaignId: string, value: Summary) { byCampaign.set(campaignId, value); notify(); }
export function getCachedCampaignApplicationDetails(id: string) { return byId.get(id) ?? null; }
export function setCachedCampaignApplicationDetails(value: MyCampaignApplicationDetails) {
  byId.set(value.id, value);
  byCampaign.set(value.campaignId, { id: value.id, status: value.status });
  notify();
}
export function updateCachedCampaignApplication(id: string, status: CampaignApplicationStatus) {
  const details = byId.get(id);
  if (details) byId.set(id, { ...details, status });
  for (const [campaignId, summary] of byCampaign) if (summary.id === id) byCampaign.set(campaignId, { ...summary, status });
  notify();
}
export function clearCampaignApplicationCache() { byCampaign.clear(); byId.clear(); notify(); }
export function subscribeCampaignApplicationCache(listener: Listener) {
  listeners.add(listener);
  return () => { listeners.delete(listener); };
}
