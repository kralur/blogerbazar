import { afterEach, describe, expect, it, vi } from "vitest";
import { api, getApiErrorMessage } from "../src/api/client";
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

  it("uses local field feedback for safe validation errors", async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify({ code: "validation_failed", errors: { Username: ["ignored"] } }), { status: 422 }));
    vi.stubGlobal("fetch", fetchMock);

    const error = await api("/api/businesses", { method: "POST", body: "{}" }).catch((reason: unknown) => reason);

    expect(getApiErrorMessage(error, "fallback", { validationMessages: { username: "Введите Telegram username" } })).toBe("Введите Telegram username");
  });
});
