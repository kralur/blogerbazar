import { act, cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider, translate } from "../src/i18n";

const api = vi.hoisted(() => ({ getFavorites: vi.fn(), getBrandFaceFavorites: vi.fn() }));
const favorites = vi.hoisted(() => ({ canManageFavorite: vi.fn() }));
let observerCallback: ((entries: Array<{ isIntersecting: boolean }>) => void) | undefined;

vi.mock("../src/api/marketplace", async (importOriginal) => ({ ...(await importOriginal<typeof import("../src/api/marketplace")>()), getFavorites: api.getFavorites, getBrandFaceFavorites: api.getBrandFaceFavorites }));
vi.mock("../src/features/favorites/FavoritesProvider", () => ({ useFavorites: () => ({ canManageFavorite: favorites.canManageFavorite, ready: true }) }));
vi.mock("../src/hooks/useScrollRestoration", () => ({ useScrollRestoration: vi.fn() }));
vi.mock("../src/hooks/useProfileDataRefresh", () => ({ useProfileDataRefresh: vi.fn() }));
vi.mock("../src/components/LanguageSwitcher", () => ({ LanguageSwitcher: () => <span>language</span> }));
vi.mock("../src/components/FavoriteButton", () => ({ FavoriteButton: () => null }));
vi.mock("../src/components/BrandFaceCard", () => ({ BrandFaceCard: ({ profile }: { profile: { name: string } }) => <article>{profile.name}</article> }));
vi.mock("../src/components/ui", () => ({ Avatar: ({ name }: { name: string }) => <span>{name}</span>, BottomNav: () => <nav />, Card: ({ children }: { children: React.ReactNode }) => <section>{children}</section>, Icon: () => <svg />, Skeleton: () => <div data-testid="skeleton" /> }));

import { Favorites } from "../src/pages/Favorites";

describe("Favorites page", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    favorites.canManageFavorite.mockImplementation((target: string) => target === "blogger" || target === "brandFace");
    api.getFavorites.mockResolvedValue({ items: [{ bloggerId: "blogger-a", name: "Amina", city: "tashkent", categories: ["beauty"], totalFollowers: 100, page: 1, pageSize: 20 }], total: 1, page: 1, pageSize: 20 });
    api.getBrandFaceFavorites.mockResolvedValue({ items: [{ id: "face-a", name: "Dilnoza", city: "tashkent-city", categories: ["beauty"], languages: ["uz"], collaborationPrice: null, avatarUrl: null, isPromoted: false }], total: 1, page: 1, pageSize: 20, hasMore: false });
    observerCallback = undefined;
    vi.stubGlobal("IntersectionObserver", class {
      constructor(callback: (entries: Array<{ isIntersecting: boolean }>) => void) { observerCallback = callback; }
      observe() {}
      disconnect() {}
    });
  });

  afterEach(() => cleanup());

  it("gives Business independent saved Blogger and Brand Face tabs without refetching preserved Blogger results", async () => {
    const user = userEvent.setup();
    render(<I18nProvider><Favorites /></I18nProvider>);
    expect((await screen.findAllByText("Amina")).length).toBeGreaterThan(0);
    await user.click(screen.getByRole("button", { name: translate("favorites.typeBrandFaces", undefined, "ru") }));
    expect(await screen.findByText("Dilnoza")).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: translate("favorites.typeBloggers", undefined, "ru") }));
    expect(screen.getAllByText("Amina").length).toBeGreaterThan(0);
    expect(api.getFavorites).toHaveBeenCalledTimes(1);
  });

  it("keeps the Brand Face segment hidden when the active role is not Business", async () => {
    favorites.canManageFavorite.mockImplementation((target: string) => target === "blogger");
    render(<I18nProvider><Favorites /></I18nProvider>);
    await waitFor(() => expect(api.getFavorites).toHaveBeenCalled());
    expect(screen.queryByRole("button", { name: translate("favorites.typeBrandFaces", undefined, "ru") })).not.toBeInTheDocument();
    expect(api.getBrandFaceFavorites).not.toHaveBeenCalled();
  });

  it("paginates Brand Face favorites without mixing the preserved Blogger state", async () => {
    api.getBrandFaceFavorites
      .mockResolvedValueOnce({ items: [{ id: "face-a", name: "Dilnoza", city: "tashkent-city", categories: ["beauty"], languages: ["uz"], collaborationPrice: null, avatarUrl: null, isPromoted: false }], total: 40, page: 1, pageSize: 20, hasMore: true })
      .mockResolvedValueOnce({ items: [{ id: "face-b", name: "Madina", city: "samarkand", categories: ["food"], languages: ["ru"], collaborationPrice: null, avatarUrl: null, isPromoted: false }], total: 40, page: 2, pageSize: 20, hasMore: false });
    const user = userEvent.setup();
    render(<I18nProvider><Favorites /></I18nProvider>);
    expect((await screen.findAllByText("Amina")).length).toBeGreaterThan(0);
    await user.click(screen.getByRole("button", { name: translate("favorites.typeBrandFaces", undefined, "ru") }));
    await screen.findByText("Dilnoza");
    await waitFor(() => expect(observerCallback).toBeDefined());
    await act(async () => observerCallback?.([{ isIntersecting: true }]));
    expect(await screen.findByText("Madina")).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: translate("favorites.typeBloggers", undefined, "ru") }));
    expect(screen.getAllByText("Amina").length).toBeGreaterThan(0);
  });

  it("shows a retry state for a Brand Face favorites API failure instead of an empty state", async () => {
    api.getBrandFaceFavorites.mockRejectedValueOnce(new Error("server unavailable"));
    const user = userEvent.setup();
    render(<I18nProvider><Favorites /></I18nProvider>);

    await user.click(screen.getByRole("button", { name: translate("favorites.typeBrandFaces", undefined, "ru") }));

    expect(await screen.findByText(translate("favorites.brandFacesLoadFailedTitle", undefined, "ru"))).toBeInTheDocument();
    expect(screen.queryByText(translate("favorites.brandFacesEmptyTitle", undefined, "ru"))).not.toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: translate("common.retry", undefined, "ru") }));
    await waitFor(() => expect(api.getBrandFaceFavorites).toHaveBeenCalledTimes(2));
    expect(api.getFavorites).toHaveBeenCalledTimes(1);
  });
});
