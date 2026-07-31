import { describe, expect, it } from "vitest";
import { normalizeMarketplaceRole } from "../src/api/marketplace";

describe("marketplace role normalization", () => {
  it("normalizes the numeric enum returned by ASP.NET Core", () => {
    expect(normalizeMarketplaceRole(0)).toBe("Blogger");
    expect(normalizeMarketplaceRole(1)).toBe("BrandFace");
    expect(normalizeMarketplaceRole(2)).toBe("Business");
  });
});
