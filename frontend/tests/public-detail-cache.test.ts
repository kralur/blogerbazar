import { afterEach, describe, expect, it, vi } from "vitest";
import { clearPublicDetailCache, getCachedPublicDetail, setCachedPublicDetail } from "../src/data/publicDetailCache";

describe("public detail cache", () => {
  afterEach(() => {
    clearPublicDetailCache();
    vi.useRealTimers();
  });

  it("separates public detail entries by resource type", () => {
    setCachedPublicDetail("blogger", "same-id", { name: "Blogger" });
    setCachedPublicDetail("campaign", "same-id", { title: "Campaign" });

    expect(getCachedPublicDetail<{ name: string }>("blogger", "same-id")).toEqual({ name: "Blogger" });
    expect(getCachedPublicDetail<{ title: string }>("campaign", "same-id")).toEqual({ title: "Campaign" });
  });

  it("keeps expired entries briefly for stale-while-revalidate, then removes them", () => {
    vi.useFakeTimers();
    setCachedPublicDetail("brand-face", "profile", { name: "Amina" });
    vi.advanceTimersByTime(60_001);

    expect(getCachedPublicDetail("brand-face", "profile")).toEqual({ name: "Amina" });
    vi.advanceTimersByTime(4 * 60_000);
    expect(getCachedPublicDetail("brand-face", "profile")).toBeNull();
  });

  it("bounds the public cache instead of growing indefinitely", () => {
    for (let index = 0; index <= 100; index += 1) setCachedPublicDetail("campaign", String(index), { index });

    expect(getCachedPublicDetail("campaign", "0")).toBeNull();
    expect(getCachedPublicDetail("campaign", "100")).toEqual({ index: 100 });
  });
});
