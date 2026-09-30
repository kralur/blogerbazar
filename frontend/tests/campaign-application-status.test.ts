import { describe, expect, it } from "vitest";
import { CampaignApplicationStatus, campaignApplicationStatusLabelKey, canAcceptCampaignApplication, canWithdrawCampaignApplication } from "../src/lib/campaignApplicationStatus";

describe("campaign application lifecycle presentation", () => {
  it("uses one shared status policy for labels and pending actions", () => {
    expect(canAcceptCampaignApplication(CampaignApplicationStatus.Sent)).toBe(true);
    expect(canWithdrawCampaignApplication(CampaignApplicationStatus.Viewed)).toBe(true);
    expect(canWithdrawCampaignApplication(CampaignApplicationStatus.Accepted)).toBe(false);
    expect(canAcceptCampaignApplication(CampaignApplicationStatus.Rejected)).toBe(false);
    expect(canAcceptCampaignApplication(CampaignApplicationStatus.Withdrawn)).toBe(false);
    expect(campaignApplicationStatusLabelKey(CampaignApplicationStatus.Accepted)).toBe("applications.status.accepted");
  });
});
