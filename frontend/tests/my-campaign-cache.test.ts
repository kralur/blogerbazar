import { afterEach, describe, expect, it } from "vitest";
import { clearMyCampaignCache, getCachedMyCampaign, setCachedMyCampaign } from "../src/data/myCampaignCache";
import { clearCampaignApplicationCache, getCachedCampaignApplication, getCachedCampaignApplicationDetails, setCachedCampaignApplication, setCachedCampaignApplicationDetails, updateCachedCampaignApplication } from "../src/data/campaignApplicationCache";
import { CampaignApplicationStatus } from "../src/lib/campaignApplicationStatus";

const accountACampaign = { id: "campaign", title: "Account A campaign" } as never;
const accountBCampaign = { id: "campaign", title: "Account B campaign" } as never;

describe("private My Campaign cache", () => {
  afterEach(clearMyCampaignCache);

  it("does not retain Account A data after logout before Account B opens the same route", () => {
    setCachedMyCampaign(accountACampaign);
    expect(getCachedMyCampaign("campaign")).toBe(accountACampaign);

    clearMyCampaignCache();
    expect(getCachedMyCampaign("campaign")).toBeNull();

    setCachedMyCampaign(accountBCampaign);
    expect(getCachedMyCampaign("campaign")).toBe(accountBCampaign);
  });
});

describe("private application cache", () => {
  afterEach(clearCampaignApplicationCache);

  it("clears the previous session data and synchronizes a mutation by application id", () => {
    setCachedCampaignApplication("campaign-a", { id: "application-a", status: CampaignApplicationStatus.Sent });
    setCachedCampaignApplicationDetails({ id: "application-a", campaignId: "campaign-a", status: CampaignApplicationStatus.Sent } as never);
    updateCachedCampaignApplication("application-a", CampaignApplicationStatus.Withdrawn);
    expect(getCachedCampaignApplication("campaign-a")).toEqual({ id: "application-a", status: CampaignApplicationStatus.Withdrawn });
    expect(getCachedCampaignApplicationDetails("application-a")?.status).toBe(CampaignApplicationStatus.Withdrawn);
    clearCampaignApplicationCache();
    expect(getCachedCampaignApplication("campaign-a")).toBeNull();
    expect(getCachedCampaignApplicationDetails("application-a")).toBeNull();
  });

  it("does not retain private application data across repeated role or session transitions", () => {
    for (const status of [CampaignApplicationStatus.Sent, CampaignApplicationStatus.Accepted, CampaignApplicationStatus.Withdrawn]) {
      setCachedCampaignApplication("campaign-a", { id: "application-a", status });
      clearCampaignApplicationCache();
      expect(getCachedCampaignApplication("campaign-a")).toBeNull();
    }
  });
});
