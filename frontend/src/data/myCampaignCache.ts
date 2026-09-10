import type { MyCampaignDetails } from "../api/marketplace";

const entries = new Map<string, MyCampaignDetails>();

export function getCachedMyCampaign(id: string) {
  return entries.get(id) ?? null;
}

export function setCachedMyCampaign(campaign: MyCampaignDetails) {
  entries.set(campaign.id, campaign);
}

export function updateCachedMyCampaign(id: string, update: (campaign: MyCampaignDetails) => MyCampaignDetails) {
  const campaign = entries.get(id);
  if (campaign) entries.set(id, update(campaign));
}

export function removeCachedMyCampaign(id: string) {
  entries.delete(id);
}

export function clearMyCampaignCache() {
  entries.clear();
}
