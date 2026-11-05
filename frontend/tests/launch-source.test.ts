import { afterEach, describe, expect, it, vi } from "vitest";

// The ad label reaches the server once, on the first /api/users/me of the start (D56): from the bot button (bb_src)
// or from a direct link to the app (startapp -> start_param).
async function firstUsersMeUrl(search: string, startParam?: string) {
  vi.resetModules();
  window.history.replaceState({}, "", `/${search}`);
  window.Telegram = { WebApp: { initData: "signed-init-data", initDataUnsafe: startParam === undefined ? {} : { start_param: startParam } } };
  const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify({ telegramUserId: 1, firstName: "A", role: 0, isBlocked: false }), { status: 200 }));
  vi.stubGlobal("fetch", fetchMock);
  const { getCurrentPlatformUser } = await import("../src/api/marketplace");
  await getCurrentPlatformUser();
  return String(fetchMock.mock.calls[0][0]);
}

describe("Launch source", () => {
  afterEach(() => {
    vi.unstubAllGlobals();
    window.history.replaceState({}, "", "/");
  });

  it("sends the label from the bot button link", async () => {
    expect(await firstUsersMeUrl("?bb_src=Insta_Nov")).toBe("/api/users/me?source=insta_nov");
  });

  it("sends the startapp label of a direct link to the app", async () => {
    expect(await firstUsersMeUrl("", "tgads_business_1")).toBe("/api/users/me?source=tgads_business_1");
  });

  it("prefers the bot link label and drops anything that is not a label", async () => {
    expect(await firstUsersMeUrl("?bb_src=site", "tgads")).toBe("/api/users/me?source=site");
    expect(await firstUsersMeUrl("", "bad label")).toBe("/api/users/me");
    expect(await firstUsersMeUrl("", "x".repeat(33))).toBe("/api/users/me");
    expect(await firstUsersMeUrl("")).toBe("/api/users/me");
  });
});
