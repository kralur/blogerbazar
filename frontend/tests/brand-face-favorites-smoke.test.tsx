import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider, translate } from "../src/i18n";

const api = vi.hoisted(() => ({ getCurrentPlatformUser: vi.fn(), getFavorites: vi.fn(), getBrandFaceFavorites: vi.fn(), getBrandFace: vi.fn(), saveBrandFaceFavorite: vi.fn(), removeBrandFaceFavorite: vi.fn() }));

vi.mock("../src/api/marketplace", async (importOriginal) => ({
  ...(await importOriginal<typeof import("../src/api/marketplace")>()),
  getCurrentPlatformUser: api.getCurrentPlatformUser,
  getFavorites: api.getFavorites,
  getBrandFaceFavorites: api.getBrandFaceFavorites,
  getBrandFace: api.getBrandFace,
  saveBrandFaceFavorite: api.saveBrandFaceFavorite,
  removeBrandFaceFavorite: api.removeBrandFaceFavorite
}));
vi.mock("../src/telegram/TelegramProvider", () => ({ useTelegram: () => ({ haptic: { success: vi.fn() } }) }));
vi.mock("../src/components/LanguageSwitcher", () => ({ LanguageSwitcher: () => <span>language</span> }));
vi.mock("../src/components/ContactList", () => ({ ContactList: () => null, hasContacts: () => false }));
vi.mock("../src/hooks/useProfileDataRefresh", () => ({ useProfileDataRefresh: vi.fn() }));
vi.mock("../src/components/ui", () => ({
  Avatar: ({ name }: { name: string }) => <span>{name}</span>,
  Badge: ({ children }: { children: React.ReactNode }) => <span>{children}</span>,
  BottomNav: () => <nav aria-label="bottom-nav" />,
  Card: ({ children }: { children: React.ReactNode }) => <section>{children}</section>,
  ErrorState: ({ title }: { title: string }) => <div>{title}</div>,
  Icon: () => <svg />,
  LoadingState: ({ title }: { title: string }) => <div>{title}</div>,
  StatsCard: ({ value }: { value: string }) => <span>{value}</span>,
  Toast: ({ message }: { message: string }) => message ? <div role="alert">{message}</div> : null
}));

import { BrandFaceCard } from "../src/components/BrandFaceCard";
import { FavoritesProvider } from "../src/features/favorites/FavoritesProvider";
import { BrandFaceDetails } from "../src/pages/BrandFaceDetails";

const profile = { id: "face-a", name: "Dilnoza", city: "tashkent-city", categories: ["beauty"], languages: ["uz"], collaborationPrice: 250_000, avatarUrl: null, isPromoted: false, createdAtUtc: "2026-08-01T00:00:00Z" };

function renderWithBusiness(children: React.ReactNode) {
  return render(<I18nProvider><FavoritesProvider>{children}</FavoritesProvider></I18nProvider>);
}

describe("Brand Face favorite production smoke coverage", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    api.getCurrentPlatformUser.mockResolvedValue({ selectedMarketplaceRole: "Business" });
    api.getFavorites.mockResolvedValue({ items: [], total: 0, page: 1, pageSize: 50 });
    api.getBrandFaceFavorites.mockResolvedValue({ items: [], total: 0, page: 1, pageSize: 50, hasMore: false });
    api.getBrandFace.mockResolvedValue(profile);
    api.saveBrandFaceFavorite.mockResolvedValue({ isFavorite: true });
    api.removeBrandFaceFavorite.mockResolvedValue({ isFavorite: false });
  });

  afterEach(() => cleanup());

  it("shows the Brand Face action on a Search card for the active Business role", async () => {
    renderWithBusiness(<BrandFaceCard profile={profile} />);

    expect(await screen.findByRole("button", { name: translate("favorites.saveBrandFaceAria", undefined, "ru") })).toBeInTheDocument();
  });

  it("shows the same action in public Brand Face Details for the active Business role", async () => {
    renderWithBusiness(<BrandFaceDetails id={profile.id} />);

    expect(await screen.findByRole("button", { name: translate("favorites.saveBrandFaceAria", undefined, "ru") })).toBeInTheDocument();
  });

  it("synchronizes an optimistic Search save and Details removal through the shared provider", async () => {
    const user = userEvent.setup();
    renderWithBusiness(<><BrandFaceCard profile={profile} /><BrandFaceDetails id={profile.id} /></>);

    const saveButtons = await screen.findAllByRole("button", { name: translate("favorites.saveBrandFaceAria", undefined, "ru") });
    await user.click(saveButtons[0]);
    await waitFor(() => expect(api.saveBrandFaceFavorite).toHaveBeenCalledWith(profile.id));
    expect(screen.getAllByRole("button", { name: translate("favorites.removeBrandFaceAria", undefined, "ru") })).toHaveLength(2);

    await user.click(screen.getAllByRole("button", { name: translate("favorites.removeBrandFaceAria", undefined, "ru") })[1]);
    await waitFor(() => expect(api.removeBrandFaceFavorite).toHaveBeenCalledWith(profile.id));
    expect(screen.getAllByRole("button", { name: translate("favorites.saveBrandFaceAria", undefined, "ru") })).toHaveLength(2);
  });
});
