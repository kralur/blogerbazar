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

export const canWithdrawCampaignApplication = canAcceptCampaignApplication;

// receiver: the company that got the application; "sent" from its side reads as "new".
export function campaignApplicationStatusLabelKey(status: CampaignApplicationStatus, receiver = false) {
  if (status === CampaignApplicationStatus.Sent) return receiver ? "applications.status.new" : "applications.status.sent";
  if (status === CampaignApplicationStatus.Viewed) return "applications.status.viewed";
  if (status === CampaignApplicationStatus.Accepted) return "applications.status.accepted";
  if (status === CampaignApplicationStatus.Rejected) return "applications.status.rejected";
  return "applications.status.withdrawn";
}

export function campaignApplicationStatusTone(status: CampaignApplicationStatus) {
  if (status === CampaignApplicationStatus.Accepted) return "green" as const;
  if (status === CampaignApplicationStatus.Rejected) return "red" as const;
  if (status === CampaignApplicationStatus.Withdrawn) return "gray" as const;
  return "blue" as const;
}
