import { describe, expect, it } from "vitest";
import { safeExternalUrl } from "../src/lib/contacts";

describe("safe external url", () => {
  it("opens only plain https links", () => {
    expect(safeExternalUrl("lumi.uz")).toBe("https://lumi.uz/");
    expect(safeExternalUrl("javascript:alert(1)")).toBeNull();
    expect(safeExternalUrl("http://lumi.uz")).toBeNull();
    expect(safeExternalUrl("https://bank.uz@evil.com")).toBeNull();
    expect(safeExternalUrl("https://user:pass@evil.com")).toBeNull();
  });
});
