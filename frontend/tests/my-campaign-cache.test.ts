import { afterEach, describe, expect, it } from "vitest";
import { clearMyCampaignCache, getCachedMyCampaign, setCachedMyCampaign } from "../src/data/myCampaignCache";

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
