import type { DealSourceType } from "../api/marketplace";

export const DealStatus = {
  Active: 0,
  Completed: 1,
  Cancelled: 2
} as const;

export function dealStatusLabelKey(status: number) {
  if (status === DealStatus.Active) return "requests.dealActive";
  if (status === DealStatus.Completed) return "requests.dealCompleted";
  return "requests.dealCancelled";
}

export function dealStatusTone(status: number) {
  if (status === DealStatus.Active) return "blue" as const;
  if (status === DealStatus.Completed) return "green" as const;
  return "gray" as const;
}

export function dealSourceLabelKey(sourceType: DealSourceType | undefined) {
  return sourceType === "collaborationRequest" ? "deals.source.collaborationRequest" : "deals.source.campaignApplication";
}

export function dealRoute(id: string) {
  return `/deal/${id}`;
}
