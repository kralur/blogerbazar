import { act, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { ApiError } from "../src/api/client";
import { I18nProvider, translate } from "../src/i18n";
import { useTelegramBackHandler } from "../src/hooks/useTelegramBackHandler";
import { MyCampaignDetails } from "../src/pages/MyCampaignDetails";
import { MyCampaigns } from "../src/pages/MyCampaigns";
import { TelegramProvider } from "../src/telegram/TelegramProvider";

const api = vi.hoisted(() => ({
  getCurrentPlatformUser: vi.fn(),
  getMyCampaigns: vi.fn(),
  getMyCampaign: vi.fn(),
  normalizeMarketplaceRole: vi.fn((role: unknown) => role)
}));

vi.mock("../src/api/marketplace", async (importOriginal) => ({
  ...(await importOriginal<typeof import("../src/api/marketplace")>()),
  getCurrentPlatformUser: api.getCurrentPlatformUser,
  getMyCampaigns: api.getMyCampaigns,
  getMyCampaign: api.getMyCampaign,
  normalizeMarketplaceRole: api.normalizeMarketplaceRole
}));

const campaign = {
  id: "campaign-a",
  title: "Coffee launch",
  city: "tashkent",
  categories: ["food"],
  minBudget: 500_000,
  maxBudget: 1_500_000,
  deadline: "2026-08-31T00:00:00Z",
  status: 1 as const,
  isPromoted: false,
  createdAtUtc: "2026-08-20T00:00:00Z",
  updatedAtUtc: "2026-08-21T00:00:00Z",
  applicationsCount: 0,
  description: "Launch coffee",
  requirements: ["Reels"]
};

function ManagementRoute({ children, onBack }: { children: React.ReactNode; onBack?: () => void }) {
  useTelegramBackHandler(onBack ?? (() => undefined));
  return <>{children}</>;
}

function renderRoute(children: React.ReactNode, onBack?: () => void) {
  return render(<I18nProvider><TelegramProvider><ManagementRoute onBack={onBack}>{children}</ManagementRoute></TelegramProvider></I18nProvider>);
}

function createEmbeddedTelegram() {
  const handlers = new Set<() => void>();
  const backButton = {
    hide: vi.fn(),
    show: vi.fn(),
    onClick: vi.fn((handler: () => void) => handlers.add(handler)),
    offClick: vi.fn((handler: () => void) => handlers.delete(handler))
  };
  window.Telegram = { WebApp: {
    initData: "signed-init-data",
    platform: "ios",
    colorScheme: "light",
    ready: vi.fn(),
    expand: vi.fn(),
    requestFullscreen: vi.fn(),
    disableVerticalSwipes: vi.fn(),
    MainButton: { hide: vi.fn() },
    SettingsButton: { hide: vi.fn() },
    BackButton: backButton
  } };
  return backButton;
}

describe("My Campaigns Back policy", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    window.IntersectionObserver = class {
      observe() {}
      disconnect() {}
      unobserve() {}
      takeRecords() { return []; }
      root = null;
      rootMargin = "";
      thresholds = [];
    } as unknown as typeof IntersectionObserver;
    api.getCurrentPlatformUser.mockResolvedValue({ selectedMarketplaceRole: "Business" });
    api.getMyCampaigns.mockResolvedValue({ items: [campaign], total: 1, page: 1, pageSize: 20, hasMore: false });
    api.getMyCampaign.mockResolvedValue(campaign);
  });

  afterEach(() => {
    delete window.Telegram;
  });

  it("uses one native BackButton and no internal Back link for the embedded list route", async () => {
    const backButton = createEmbeddedTelegram();
    const onBack = vi.fn();
    const view = renderRoute(<MyCampaigns />, onBack);

    await screen.findByText("Coffee launch");
    expect(document.documentElement.dataset.telegramEmbedded).toBe("true");
    expect(screen.queryByRole("link", { name: translate("myCampaigns.backAria", undefined, "ru") })).not.toBeInTheDocument();
    await waitFor(() => expect(backButton.show).toHaveBeenCalledTimes(1));
    expect(backButton.onClick).toHaveBeenCalledTimes(1);

    const nativeBack = backButton.onClick.mock.calls[0][0] as () => void;
    act(nativeBack);
    expect(onBack).toHaveBeenCalledTimes(1);

    view.rerender(<I18nProvider><TelegramProvider><ManagementRoute onBack={onBack}><MyCampaigns /></ManagementRoute></TelegramProvider></I18nProvider>);
    expect(backButton.onClick).toHaveBeenCalledTimes(1);
    view.unmount();
    expect(backButton.offClick).toHaveBeenCalledWith(nativeBack);
  });

  it("keeps the owner details route free of an internal Back link in embedded Telegram", async () => {
    createEmbeddedTelegram();
    renderRoute(<MyCampaignDetails id="campaign-a" />);

    await screen.findByText("Launch coffee");
    expect(document.documentElement.dataset.telegramEmbedded).toBe("true");
    expect(screen.queryByRole("link", { name: translate("myCampaignDetails.backAria", undefined, "ru") })).not.toBeInTheDocument();
  });

  it("renders one hash-navigation fallback Back link for standalone list and details routes", async () => {
    const listView = renderRoute(<MyCampaigns />);
    await screen.findByText("Coffee launch");
    const listBack = await screen.findByRole("link", { name: translate("myCampaigns.backAria", undefined, "ru") });
    expect(listBack).toHaveAttribute("href", "#/profile");
    fireEvent.click(listBack);
    expect(window.location.hash).toBe("#/profile");
    listView.unmount();

    window.location.hash = "#/my-campaign/campaign-a";
    const detailsView = renderRoute(<MyCampaignDetails id="campaign-a" />);
    await screen.findByText("Launch coffee");
    const detailsBack = await screen.findByRole("link", { name: translate("myCampaignDetails.backAria", undefined, "ru") });
    expect(detailsBack).toHaveAttribute("href", "#/my-campaigns");
    fireEvent.click(detailsBack);
    expect(window.location.hash).toBe("#/my-campaigns");
    detailsView.unmount();
  });

  it("does not turn owner detail authorization failures into a public route fallback", async () => {
    api.getMyCampaign.mockRejectedValueOnce(new ApiError(403));
    renderRoute(<MyCampaignDetails id="campaign-a" />);

    await screen.findByText(translate("myCampaignDetails.deniedTitle", undefined, "ru"));
    expect(screen.queryByText("Coffee launch")).not.toBeInTheDocument();
  });
});
