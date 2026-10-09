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
  setProfileVisibility: vi.fn(),
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

  it("links an approved profile to its public page and hides the link while it waits for review", async () => {
    api.getMyBusinessProfile.mockResolvedValue({ id: "business-a", name: "Lumi Beauty", city: "tashkent-city", moderationStatus: 1 });
    const { unmount } = render(<I18nProvider><ProfileDashboard /></I18nProvider>);
    expect(await screen.findByRole("link", { name: translate("profile.viewPublic", undefined, "ru") })).toHaveAttribute("href", "#/company/business-a");
    unmount();

    api.getMyBusinessProfile.mockResolvedValue({ id: "business-a", name: "Lumi Beauty", city: "tashkent-city", moderationStatus: 0 });
    renderDashboard();
    await screen.findByText("Lumi Beauty");
    expect(screen.queryByRole("link", { name: translate("profile.viewPublic", undefined, "ru") })).not.toBeInTheDocument();
  });

  // The Telegram photo shown on top is only a stand-in: others see a letter until the profile has its own image.
  it("tells a business without a logo that others see only a letter, and hides the hint once a logo exists", async () => {
    const { unmount } = render(<I18nProvider><ProfileDashboard /></I18nProvider>);
    expect(await screen.findByText(translate("profile.logoMissingHint", undefined, "ru"))).toBeInTheDocument();
    unmount();

    api.getMyBusinessProfile.mockResolvedValue({ id: "business-a", name: "Lumi Beauty", city: "tashkent-city", logoUrl: "https://cdn.example/logo.webp", moderationStatus: 1 });
    renderDashboard();
    await screen.findByText("Lumi Beauty");
    expect(screen.queryByText(translate("profile.logoMissingHint", undefined, "ru"))).not.toBeInTheDocument();
  });

  // D50 and the role header: the top card shows the selected role as others see it, and the role can be paused.
  it("shows the selected role's name on top instead of the Telegram name, without the Telegram photo", async () => {
    renderDashboard();
    expect(await screen.findByText("Lumi Beauty")).toBeInTheDocument();
    expect(screen.queryByText("Umid")).not.toBeInTheDocument();
    expect(document.querySelector('img[src="https://telegram.example/avatar.jpg"]')).toBeNull();
  });

  it("hides the role profile after confirmation and shows it again in one tap", async () => {
    const user = userEvent.setup();
    // Like the server: the reload after a change returns the new state.
    let hidden = false;
    api.getMyBusinessProfile.mockImplementation(async () => ({ id: "business-a", name: "Lumi Beauty", city: "tashkent-city", logoUrl: "https://cdn.example/logo.webp", moderationStatus: 1, isHidden: hidden }));
    api.setProfileVisibility.mockImplementation(async (_target: string, next: boolean) => { hidden = next; return { role: 2, isHidden: next }; });
    renderDashboard();
    await user.click(await screen.findByRole("button", { name: translate("profile.hide", undefined, "ru") }));
    expect(api.setProfileVisibility).not.toHaveBeenCalled();
    await user.click(screen.getByRole("button", { name: translate("profile.hideConfirm", undefined, "ru") }));

    await waitFor(() => expect(api.setProfileVisibility).toHaveBeenCalledWith("business", true));
    expect(await screen.findByText(translate("profile.hidden", undefined, "ru"))).toBeInTheDocument();
    expect(screen.getByText(translate("profile.hiddenDescription", undefined, "ru"))).toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: translate("profile.show", undefined, "ru") }));
    await waitFor(() => expect(api.setProfileVisibility).toHaveBeenLastCalledWith("business", false));
    expect(await screen.findByText(translate("profile.approved", undefined, "ru"))).toBeInTheDocument();
  });

  it("keeps the profile visible and explains when hiding fails", async () => {
    const user = userEvent.setup();
    api.setProfileVisibility.mockRejectedValue(new ApiError(500));
    renderDashboard();
    await user.click(await screen.findByRole("button", { name: translate("profile.hide", undefined, "ru") }));
    await user.click(screen.getByRole("button", { name: translate("profile.hideConfirm", undefined, "ru") }));

    await waitFor(() => expect(api.setProfileVisibility).toHaveBeenCalled());
    expect(screen.queryByText(translate("profile.hidden", undefined, "ru"))).not.toBeInTheDocument();
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
