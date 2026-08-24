import { afterEach, describe, expect, it, vi } from "vitest";
import { api, getApiErrorMessage } from "../src/api/client";
import { translate } from "../src/i18n";
import { getBrandFaceCatalog, getBrandFaceFavorites, getCampaignCatalog, getCampaigns, removeBrandFaceFavorite, saveBrandFaceFavorite } from "../src/api/marketplace";

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

  it("uses the Brand Face catalog endpoint with only its supported query parameters", async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify({ items: [], total: 0, page: 1, pageSize: 20, hasMore: false }), { status: 200 }));
    vi.stubGlobal("fetch", fetchMock);
    const controller = new AbortController();

    await getBrandFaceCatalog({ query: "Dilnoza", city: "tashkent-city", category: "beauty", language: "Русский", minPrice: 100_000, maxPrice: 300_000, sort: "price_asc", page: 1, pageSize: 20 }, controller.signal);

    expect(fetchMock.mock.calls[0][0]).toBe("/api/brand-faces/catalog?query=Dilnoza&city=tashkent-city&category=beauty&language=%D0%A0%D1%83%D1%81%D1%81%D0%BA%D0%B8%D0%B9&minPrice=100000&maxPrice=300000&sort=price_asc&page=1&pageSize=20");
    expect(fetchMock.mock.calls[0][1]).toMatchObject({ signal: controller.signal });
  });

  it("uses additive Brand Face Favorites routes without changing Blogger Favorites routes", async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(new Response(JSON.stringify({ items: [], total: 0, page: 1, pageSize: 20, hasMore: false }), { status: 200 }))
      .mockResolvedValueOnce(new Response(JSON.stringify({ isFavorite: true }), { status: 200 }))
      .mockResolvedValueOnce(new Response(JSON.stringify({ isFavorite: false }), { status: 200 }));
    vi.stubGlobal("fetch", fetchMock);

    await getBrandFaceFavorites();
    await saveBrandFaceFavorite("face-a");
    await removeBrandFaceFavorite("face-a");

    expect(fetchMock.mock.calls.map(([url]) => url)).toEqual([
      "/api/favorites/brand-faces?page=1&pageSize=20",
      "/api/favorites/brand-faces/face-a",
      "/api/favorites/brand-faces/face-a"
    ]);
    expect(fetchMock.mock.calls[1][1]).toMatchObject({ method: "POST" });
    expect(fetchMock.mock.calls[2][1]).toMatchObject({ method: "DELETE" });
  });

  it("uses the paged Campaign Catalog contract without changing the legacy Campaign client", async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(new Response(JSON.stringify({ items: [{ id: "campaign-a", title: "Campaign", businessName: "Business", businessAvatarUrl: null, city: null, categories: ["beauty"], requirements: null, minBudget: null, maxBudget: null, deadline: null, status: 1, isPromoted: false, createdAtUtc: "2026-08-24T00:00:00Z" }], total: 1, page: 2, pageSize: 10, hasMore: false }), { status: 200 }))
      .mockResolvedValueOnce(new Response(JSON.stringify({ campaigns: [] }), { status: 200 }));
    vi.stubGlobal("fetch", fetchMock);
    const controller = new AbortController();

    await expect(getCampaignCatalog({ query: "Coffee", city: "tashkent-city", category: "beauty", minBudget: 100_000, maxBudget: 300_000, deadlineFrom: "2026-08-01", deadlineTo: "2026-08-31", sort: "budget_desc", page: 2, pageSize: 10 }, controller.signal)).resolves.toMatchObject({
      items: [{ city: null, businessAvatarUrl: null, minBudget: null, maxBudget: null, deadline: null }],
      total: 1,
      page: 2,
      pageSize: 10,
      hasMore: false
    });
    await getCampaigns();

    expect(fetchMock.mock.calls[0][0]).toBe("/api/campaigns/catalog?query=Coffee&city=tashkent-city&category=beauty&minBudget=100000&maxBudget=300000&deadlineFrom=2026-08-01&deadlineTo=2026-08-31&sort=budget_desc&page=2&pageSize=10");
    expect(fetchMock.mock.calls[0][1]).toMatchObject({ signal: controller.signal });
    expect(fetchMock.mock.calls[1][0]).toBe("/api/campaigns?pageSize=20");
  });
});
