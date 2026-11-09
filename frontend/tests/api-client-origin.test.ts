import { afterEach, describe, expect, it, vi } from "vitest";

// D64 stage B: inside Telegram the Mini App calls the API's own address (VITE_API_URL); in a browser it calls /api/ on
// the page's address, whose nginx passes it to the API, so the HttpOnly session cookie stays first-party.
describe("API address", () => {
  afterEach(() => {
    vi.unstubAllEnvs();
    vi.unstubAllGlobals();
    vi.resetModules();
    delete window.Telegram;
  });

  async function firstUrl(inTelegram: boolean) {
    vi.stubEnv("VITE_API_URL", "https://api.example.test");
    if (inTelegram) window.Telegram = { WebApp: { initData: "signed-init-data" } };
    const fetchMock = vi.fn().mockResolvedValue(new Response("{}", { status: 200 }));
    vi.stubGlobal("fetch", fetchMock);
    const { api } = await import("../src/api/client");
    await api("/api/users/me");
    return fetchMock.mock.calls[0][0] as string;
  }

  it("keeps the API's own address inside Telegram", async () => {
    expect(await firstUrl(true)).toBe("https://api.example.test/api/users/me");
  });

  it("uses the page's own address in a browser", async () => {
    expect(await firstUrl(false)).toBe("/api/users/me");
  });
});
