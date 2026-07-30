import { afterEach, describe, expect, it, vi } from "vitest";
import { api } from "../src/api/client";
import { translate } from "../src/i18n";

describe("API client", () => {
  afterEach(() => vi.unstubAllGlobals());

  it("sends Telegram init data using the existing authorization scheme", async () => {
    window.Telegram = { WebApp: { initData: "signed-init-data" } };
    const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify({ id: 7 }), { status: 200 }));
    vi.stubGlobal("fetch", fetchMock);

    await expect(api<{ id: number }>("/api/v1/profile")).resolves.toEqual({ id: 7 });

    const [, request] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(fetchMock.mock.calls[0][0]).toBe("/api/v1/profile");
    expect(new Headers(request.headers).get("authorization")).toBe("tma signed-init-data");
  });

  it("maps safe API errors to localized messages", async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify({ code: "access_denied" }), { status: 403 }));
    vi.stubGlobal("fetch", fetchMock);

    await expect(api("/api/v1/admin")).rejects.toThrow(translate("error.access_denied", undefined, "ru"));
  });
});
