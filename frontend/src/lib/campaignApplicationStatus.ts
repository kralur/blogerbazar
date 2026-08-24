export const CampaignApplicationStatus = {
  Sent: 0,
  Viewed: 1,
  Accepted: 2,
  Rejected: 3,
  Withdrawn: 4
} as const;

export type CampaignApplicationStatus = typeof CampaignApplicationStatus[keyof typeof CampaignApplicationStatus];

export function canAcceptCampaignApplication(status: CampaignApplicationStatus) {
  return status === CampaignApplicationStatus.Sent || status === CampaignApplicationStatus.Viewed;
}

export function campaignApplicationStatusTone(status: CampaignApplicationStatus) {
  if (status === CampaignApplicationStatus.Accepted) return "green" as const;
  if (status === CampaignApplicationStatus.Rejected) return "red" as const;
  if (status === CampaignApplicationStatus.Withdrawn) return "gray" as const;
  return "blue" as const;
}
