import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { ApiError } from "../src/api/client";
import { I18nProvider, translate } from "../src/i18n";

const api = vi.hoisted(() => ({
  deleteCurrentAccount: vi.fn(),
  deleteProfileImage: vi.fn(),
  getCurrentPlatformUser: vi.fn(),
  getMyBloggerProfile: vi.fn(),
  getMyBrandFaceProfile: vi.fn(),
  getMyBusinessProfile: vi.fn(),
  getMyCampaignApplications: vi.fn(),
  getMyDeals: vi.fn(),
  normalizeMarketplaceRole: (role: string | null) => role,
  selectMarketplaceRole: vi.fn(),
  uploadProfileImage: vi.fn()
}));
const telegram = vi.hoisted(() => ({
  haptic: { error: vi.fn(), selection: vi.fn(), success: vi.fn(), warning: vi.fn() },
  registerBackButtonHandler: vi.fn(() => vi.fn()),
  setBackButtonHandler: vi.fn(),
  setClosingConfirmation: vi.fn(),
  user: { first_name: "Umid", photo_url: "https://telegram.example/avatar.jpg", username: "umidkb" }
}));

vi.mock("../src/api/marketplace", () => api);
vi.mock("../src/telegram/TelegramProvider", () => ({ useTelegram: () => telegram }));
vi.mock("../src/features/favorites/FavoritesProvider", () => ({ useFavorites: () => ({ refreshFavorites: vi.fn() }) }));

import { ProfileDashboard } from "../src/pages/ProfileDashboard";
import { Settings } from "../src/pages/Settings";

function renderDashboard() {
  render(<I18nProvider><ProfileDashboard /></I18nProvider>);
}

// Logout and account deletion live in Settings (D38).
async function renderSettings(onSessionReset = vi.fn()) {
  render(<I18nProvider><Settings onSessionReset={onSessionReset} /></I18nProvider>);
  await screen.findByRole("button", { name: translate("profile.logout", undefined, "ru") });
  return onSessionReset;
}

async function openDeleteDialog(user: ReturnType<typeof userEvent.setup>) {
  await user.click(screen.getByRole("button", { name: translate("profile.deleteAccount", undefined, "ru") }));
  await screen.findByText(translate("profile.deleteAccountDescription", undefined, "ru"));
}

describe("Profile dashboard account flows", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    api.getCurrentPlatformUser.mockResolvedValue({ selectedMarketplaceRole: "Business" });
    api.getMyBloggerProfile.mockRejectedValue(new ApiError(404));
    api.getMyBrandFaceProfile.mockRejectedValue(new ApiError(404));
    api.getMyBusinessProfile.mockResolvedValue({ name: "Lumi Beauty", city: "tashkent-city", description: "Beauty", phone: "+998 90 123 45 67", email: null, logoUrl: null, moderationStatus: 1 });
    api.getMyCampaignApplications.mockResolvedValue([]);
    api.getMyDeals.mockResolvedValue([]);
    api.deleteCurrentAccount.mockResolvedValue({ alreadyDeleted: false });
    localStorage.clear();
    sessionStorage.clear();
  });

  afterEach(() => {
    localStorage.clear();
    sessionStorage.clear();
  });

  it("groups shortcuts and links to settings instead of holding the language switcher", async () => {
    api.getMyCampaignApplications.mockResolvedValue([{ id: "a1" }]);
    api.getMyDeals.mockResolvedValue([{ id: "d1" }, { id: "d2" }]);
    renderDashboard();
    await screen.findByText("Lumi Beauty");

    const shortcuts = screen.getByRole("navigation", { name: translate("profile.shortcuts", undefined, "ru") });
    await waitFor(() => expect(shortcuts.querySelector('a[href="#/requests"]')).toHaveTextContent(translate("profile.requestsSummary", { applications: 1, deals: 2 }, "ru")));
    expect(shortcuts.querySelector('a[href="#/my-campaigns"]')).not.toBeNull();
    expect(shortcuts.querySelector('a[href="#/favorites"]')).not.toBeNull();
    expect(screen.queryByRole("group", { name: translate("language.interface", undefined, "ru") })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: translate("profile.logout", undefined, "ru") })).not.toBeInTheDocument();
    expect(screen.getByRole("navigation", { name: translate("profile.settings", undefined, "ru") }).querySelector('a[href="#/settings"]')).not.toBeNull();
  });

  it("clears local BloggerBazar state and returns to the App reset callback after logout", async () => {
    const user = userEvent.setup();
    const onSessionReset = await renderSettings();
    localStorage.setItem("bloggerbazar.selectedRole", "business");
    sessionStorage.setItem("bloggerbazar.onboarding.media-warning", "warning");

    await user.click(screen.getByRole("button", { name: translate("profile.logout", undefined, "ru") }));
    await user.click(screen.getAllByRole("button", { name: translate("profile.logout", undefined, "ru") }).at(-1)!);

    expect(onSessionReset).toHaveBeenCalledTimes(1);
    expect(localStorage.getItem("bloggerbazar.selectedRole")).toBeNull();
    expect(sessionStorage.getItem("bloggerbazar.onboarding.media-warning")).toBeNull();
  });

  it("deletes the current account once, then clears local state and resets the app", async () => {
    const user = userEvent.setup();
    const onSessionReset = await renderSettings();
    localStorage.setItem("bloggerbazar.cache", "cached");

    await openDeleteDialog(user);
    await user.click(screen.getAllByRole("button", { name: translate("profile.deleteAccount", undefined, "ru") }).at(-1)!);

    await waitFor(() => expect(api.deleteCurrentAccount).toHaveBeenCalledTimes(1));
    expect(onSessionReset).toHaveBeenCalledTimes(1);
    expect(localStorage.getItem("bloggerbazar.cache")).toBeNull();
    expect(telegram.haptic.success).toHaveBeenCalledTimes(1);
  });

  it("keeps the dialog open and explains a failed deletion without resetting the app", async () => {
    const user = userEvent.setup();
    api.deleteCurrentAccount.mockRejectedValue(new Error("offline"));
    const onSessionReset = await renderSettings();

    await openDeleteDialog(user);
    await user.click(screen.getAllByRole("button", { name: translate("profile.deleteAccount", undefined, "ru") }).at(-1)!);

    expect(await screen.findByText(translate("profile.deleteFailed", undefined, "ru"))).toBeInTheDocument();
    expect(onSessionReset).not.toHaveBeenCalled();
    expect(telegram.haptic.error).toHaveBeenCalled();
  });
});
