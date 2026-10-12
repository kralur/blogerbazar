import { describe, expect, it } from "vitest";
import { isDeepLinkRoute } from "../src/lib/deepLink";

describe("bot deep links", () => {
  it("accepts deals, offers and the screens bot messages point to", () => {
    expect(isDeepLinkRoute("/deal/3f2504e0-4f89-11d3-9a0c-0305e82c3301")).toBe(true);
    expect(isDeepLinkRoute("/offer/3f2504e0-4f89-11d3-9a0c-0305e82c3301")).toBe(true);
    for (const route of ["/", "/search", "/campaigns", "/requests", "/profile", "/settings", "/my-campaigns", "/blogger-form", "/business", "/brand-face", "/admin"]) {
      expect(isDeepLinkRoute(route)).toBe(true);
    }
  });

  it("rejects anything else", () => {
    for (const route of [null, "", "/deal/1", "/unknown", "javascript:alert(1)", "/search?x=1", "//evil.example"]) {
      expect(isDeepLinkRoute(route)).toBe(false);
    }
  });
});
