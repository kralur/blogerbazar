import { render, screen, waitFor } from "@testing-library/react";
import type { ReactNode } from "react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider, translate } from "../src/i18n";

const api = vi.hoisted(() => ({
  getCurrentPlatformUser: vi.fn(),
  getMyBloggerProfile: vi.fn(),
  getMyBrandFaceProfile: vi.fn(),
  getMyBusinessProfile: vi.fn(),
  normalizeMarketplaceRole: vi.fn((role: string | null | undefined) => role ?? undefined),
  selectMarketplaceRole: vi.fn()
}));
const telegram = vi.hoisted(() => ({
  isTelegram: true,
  setBackButtonHandler: vi.fn(),
  haptic: { selection: vi.fn() }
}));

vi.mock("../src/api/marketplace", () => api);
vi.mock("../src/telegram/TelegramProvider", () => ({
  useTelegram: () => telegram,
  telegramBridge: { initData: "telegram-init-data" }
}));
vi.mock("../src/features/favorites/FavoritesProvider", () => ({
  FavoritesProvider: ({ children }: { children: ReactNode }) => <>{children}</>
}));
vi.mock("../src/pages/Home", () => ({ Home: () => <h1>Home screen</h1> }));
vi.mock("../src/pages/BloggerProfileForm", () => ({ BloggerProfileForm: () => <p>Blogger form</p> }));
vi.mock("../src/pages/BrandFaceProfileForm", () => ({ BrandFaceProfileForm: () => <p>Brand Face form</p> }));
vi.mock("../src/pages/BusinessProfileForm", () => ({ BusinessProfileForm: () => <p>Business form</p> }));

import { App } from "../src/App";

describe("FTUE navigation", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    window.location.hash = "#/";
    localStorage.setItem("bloggerbazar.onboarding.welcomeViewed", "true");
    api.getCurrentPlatformUser.mockResolvedValue({ selectedMarketplaceRole: "BrandFace" });
    api.getMyBrandFaceProfile.mockRejectedValue(new Error("not found"));
    api.selectMarketplaceRole.mockResolvedValue(undefined);
  });

  it("opens role selection after a new launch when the saved role has no profile", async () => {
    render(<I18nProvider><App /></I18nProvider>);

    expect(await screen.findByRole("heading", { name: translate("onboarding.title", undefined, "ru") })).toBeInTheDocument();
    expect(screen.queryByText("Brand Face form")).not.toBeInTheDocument();
  });

  it("opens Home directly only when FTUE was already completed", async () => {
    localStorage.setItem("bloggerbazar.onboarding.completed", "true");
    api.getMyBrandFaceProfile.mockResolvedValue({ id: "brand-face-profile" });
    render(<I18nProvider><App /></I18nProvider>);

    expect(await screen.findByRole("heading", { name: "Home screen" })).toBeInTheDocument();
  });

  it("opens the selected profile form only after explicit role confirmation", async () => {
    const user = userEvent.setup();
    render(<I18nProvider><App /></I18nProvider>);

    const role = await screen.findByRole("radio", { name: new RegExp(translate("onboarding.brandFace", undefined, "ru"), "i") });
    await user.click(role);
    await user.click(screen.getByRole("button", { name: translate("onboarding.selectRole", undefined, "ru") }));

    await waitFor(() => expect(api.selectMarketplaceRole).toHaveBeenCalledWith("BrandFace"));
    expect(await screen.findByText("Brand Face form")).toBeInTheDocument();
  });

  it("keeps one launch screen while the initial account lookup is pending", async () => {
    let resolveUser: ((value: { selectedMarketplaceRole: string }) => void) | undefined;
    api.getCurrentPlatformUser.mockImplementation(() => new Promise((resolve) => { resolveUser = resolve; }));
    render(<I18nProvider><App /></I18nProvider>);

    expect(await screen.findByRole("main", { name: translate("firstRun.launchLoading", undefined, "ru") })).toBeInTheDocument();
    expect(screen.queryByText("Home screen")).not.toBeInTheDocument();
    expect(api.getCurrentPlatformUser).toHaveBeenCalledTimes(1);

    resolveUser?.({ selectedMarketplaceRole: "BrandFace" });
    expect(await screen.findByRole("heading", { name: translate("onboarding.title", undefined, "ru") })).toBeInTheDocument();
  });

  it("leaves launch for the safe Telegram authorization state after an initial API error", async () => {
    api.getCurrentPlatformUser.mockRejectedValueOnce(new Error("offline"));
    render(<I18nProvider><App /></I18nProvider>);

    expect(await screen.findByRole("heading", { name: translate("firstRun.authFailedTitle", undefined, "ru") })).toBeInTheDocument();
    expect(screen.queryByRole("main", { name: translate("firstRun.launchLoading", undefined, "ru") })).not.toBeInTheDocument();
  });
});
