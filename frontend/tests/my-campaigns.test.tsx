import { act, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { ApiError } from "../src/api/client";
import { I18nProvider, translate } from "../src/i18n";

const api = vi.hoisted(() => ({
  getCurrentPlatformUser: vi.fn(),
  getMyCampaigns: vi.fn(),
  getMyCampaign: vi.fn(),
  closeMyCampaign: vi.fn(),
  normalizeMarketplaceRole: vi.fn((role: unknown) => role)
}));

let intersectionCallback: IntersectionObserverCallback | undefined;

vi.mock("../src/api/marketplace", async (importOriginal) => ({
  ...(await importOriginal<typeof import("../src/api/marketplace")>()),
  getCurrentPlatformUser: api.getCurrentPlatformUser,
  getMyCampaigns: api.getMyCampaigns,
  getMyCampaign: api.getMyCampaign,
  closeMyCampaign: api.closeMyCampaign,
  normalizeMarketplaceRole: api.normalizeMarketplaceRole
}));

vi.mock("../src/components/LanguageSwitcher", () => ({ LanguageSwitcher: () => <span>language</span> }));
vi.mock("../src/hooks/useScrollRestoration", () => ({ useScrollRestoration: () => undefined }));
vi.mock("../src/telegram/TelegramProvider", () => ({ useTelegram: () => ({ haptic: { selection: vi.fn() } }) }));
vi.mock("../src/components/ui", () => ({
  Badge: ({ children }: { children: React.ReactNode }) => <span>{children}</span>,
  BottomNav: () => <nav aria-label="bottom-nav" />,
  Button: ({ children, onClick, ...props }: React.ButtonHTMLAttributes<HTMLButtonElement>) => <button onClick={onClick} {...props}>{children}</button>,
  Card: ({ children }: { children: React.ReactNode }) => <section>{children}</section>,
  ErrorState: ({ title, subtitle, onRetry }: { title: string; subtitle: string; onRetry?: () => void }) => <section><h1>{title}</h1><p>{subtitle}</p>{onRetry && <button onClick={onRetry} type="button">retry</button>}</section>,
  Icon: () => <svg />,
  LoadingState: ({ title }: { title: string }) => <p>{title}</p>,
  Modal: ({ children, open, title }: { children: React.ReactNode; open: boolean; title: string }) => open ? <section aria-label={title}>{children}</section> : null,
  SearchBar: ({ value, onChange, onClear, placeholder, clearAriaLabel }: { value?: string; onChange?: React.ChangeEventHandler<HTMLInputElement>; onClear?: () => void; placeholder?: string; clearAriaLabel?: string }) => <><input aria-label={placeholder} onChange={onChange} value={value} />{value && onClear && <button aria-label={clearAriaLabel} onClick={onClear} type="button">clear</button>}</>,
  Skeleton: () => <div>loading</div>,
  Toast: () => null
}));

import { MyCampaignDetails } from "../src/pages/MyCampaignDetails";
import { MyCampaigns } from "../src/pages/MyCampaigns";

const campaign = {
  id: "campaign-a",
  title: "Coffee launch",
  city: "tashkent",
  categories: ["food", "beauty"],
  minBudget: 500_000,
  maxBudget: 1_500_000,
  deadline: "2026-08-31T00:00:00Z",
  status: 1 as const,
  isPromoted: true,
  createdAtUtc: "2026-08-20T00:00:00Z",
  updatedAtUtc: "2026-08-21T00:00:00Z",
  applicationsCount: 0
};

function page(overrides: Partial<{ items: typeof campaign[]; total: number; page: number; pageSize: number; hasMore: boolean }> = {}) {
  return { items: [campaign], total: 1, page: 1, pageSize: 20, hasMore: false, ...overrides };
}

function renderList() {
  return render(<I18nProvider><MyCampaigns /></I18nProvider>);
}

describe("My Campaigns management", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.useRealTimers();
    window.IntersectionObserver = class {
      constructor(callback: IntersectionObserverCallback) { intersectionCallback = callback; }
      observe() {}
      disconnect() {}
      unobserve() {}
      takeRecords() { return []; }
      root = null;
      rootMargin = "";
      thresholds = [];
    } as unknown as typeof IntersectionObserver;
    api.getCurrentPlatformUser.mockResolvedValue({ selectedMarketplaceRole: "Business" });
    api.getMyCampaigns.mockResolvedValue(page());
    api.getMyCampaign.mockResolvedValue({ ...campaign, description: "Launch coffee", requirements: ["Reels"] });
  });

  afterEach(() => vi.useRealTimers());

  it("loads the private list for an active Business role with its own cache/query contract", async () => {
    renderList();

    await screen.findByText("Coffee launch");
    expect(document.querySelector(".campaign-management-screen")).toBeInTheDocument();
    expect(document.querySelector(".my-campaigns__header")).toBeInTheDocument();
    expect(screen.queryByRole("link", { name: translate("myCampaigns.backAria", undefined, "ru") })).not.toBeInTheDocument();
    expect(api.getMyCampaigns).toHaveBeenCalledWith({ page: 1, pageSize: 20, query: undefined, sort: "newest", status: undefined }, expect.any(AbortSignal));
    expect(screen.getByRole("link", { name: translate("myCampaigns.openAria", { title: "Coffee launch" }, "ru") })).toHaveAttribute("href", "#/my-campaign/campaign-a");
    expect(screen.getByText("0 заявок")).toBeInTheDocument();
    expect(screen.getByText("от 500", { exact: false })).toBeInTheDocument();
  });

  it("does not fetch or expose management controls outside the active Business role", async () => {
    api.getCurrentPlatformUser.mockResolvedValue({ selectedMarketplaceRole: "Blogger" });
    renderList();

    await screen.findByText(translate("myCampaigns.deniedTitle", undefined, "ru"));
    expect(api.getMyCampaigns).not.toHaveBeenCalled();
    expect(screen.queryByRole("link", { name: translate("myCampaigns.create", undefined, "ru") })).not.toBeInTheDocument();
  });

  it("resets pagination when the server-side status filter changes and keeps no BusinessId in the request", async () => {
    renderList();
    await screen.findByText("Coffee launch");

    fireEvent.change(screen.getByLabelText(translate("myCampaigns.statusFilter", undefined, "ru")), { target: { value: "1" } });
    await waitFor(() => expect(api.getMyCampaigns).toHaveBeenLastCalledWith({ page: 1, pageSize: 20, query: undefined, sort: "newest", status: 1 }, expect.any(AbortSignal)));
    expect(api.getMyCampaigns.mock.calls.at(-1)?.[0]).not.toHaveProperty("businessId");
  });

  it("debounces search before sending a new private request", async () => {
    renderList();
    await screen.findByText("Coffee launch");
    const initialCalls = api.getMyCampaigns.mock.calls.length;

    fireEvent.change(screen.getByLabelText(translate("myCampaigns.searchPlaceholder", undefined, "ru")), { target: { value: " Coffee " } });
    expect(api.getMyCampaigns).toHaveBeenCalledTimes(initialCalls);
    await waitFor(() => expect(api.getMyCampaigns).toHaveBeenLastCalledWith({ page: 1, pageSize: 20, query: "Coffee", sort: "newest", status: undefined }, expect.any(AbortSignal)), { timeout: 1_000 });
  });

  it("appends the next page once and removes duplicate campaign ids", async () => {
    const nextCampaign = { ...campaign, id: "campaign-b", title: "Tea launch" };
    api.getMyCampaigns
      .mockResolvedValueOnce(page({ hasMore: true }))
      .mockResolvedValueOnce(page({ items: [campaign, nextCampaign], total: 2, page: 2, hasMore: false }));
    renderList();
    await screen.findByText("Coffee launch");

    await act(async () => { intersectionCallback?.([{ isIntersecting: true } as IntersectionObserverEntry], {} as IntersectionObserver); });
    await screen.findByText("Tea launch");
    expect(api.getMyCampaigns).toHaveBeenLastCalledWith({ page: 2, pageSize: 20, query: undefined, sort: "newest", status: undefined }, expect.any(AbortSignal));
    expect(screen.getAllByRole("link", { name: translate("myCampaigns.openAria", { title: "Coffee launch" }, "ru") })).toHaveLength(1);
  });

  it("keeps a filtered empty result distinct from a first-campaign empty state", async () => {
    api.getMyCampaigns.mockResolvedValue(page({ items: [], total: 0 }));
    renderList();
    await screen.findByText(translate("myCampaigns.emptyTitle", undefined, "ru"));
    expect(screen.getByRole("button", { name: translate("myCampaigns.create", undefined, "ru") })).toBeInTheDocument();

    fireEvent.change(screen.getByLabelText(translate("myCampaigns.statusFilter", undefined, "ru")), { target: { value: "1" } });
    await screen.findByText(translate("myCampaigns.filteredEmptyTitle", undefined, "ru"));
    expect(screen.getAllByRole("button", { name: translate("myCampaigns.clearFilters", undefined, "ru") })).toHaveLength(2);
  });

  it("retries a recoverable list error without navigation fallback", async () => {
    api.getMyCampaigns.mockRejectedValueOnce(new Error("network")).mockResolvedValue(page());
    renderList();
    await screen.findByText(translate("myCampaigns.errorTitle", undefined, "ru"));
    fireEvent.click(screen.getByRole("button", { name: translate("common.retry", undefined, "ru") }));
    await screen.findByText("Coffee launch");
    expect(api.getMyCampaigns).toHaveBeenCalledTimes(2);
  });

  it("uses only the private owner details endpoint and handles 404 without public fallback", async () => {
    api.getMyCampaign.mockRejectedValueOnce(new ApiError(404));
    render(<I18nProvider><MyCampaignDetails id="missing" /></I18nProvider>);

    await screen.findByText(translate("myCampaignDetails.notFoundTitle", undefined, "ru"));
    expect(api.getMyCampaign).toHaveBeenCalledWith("missing", expect.any(AbortSignal));
    expect(screen.queryByText("Launch coffee")).not.toBeInTheDocument();
  });

  it("renders real owner detail fields and management actions only for an active campaign", async () => {
    render(<I18nProvider><MyCampaignDetails id="campaign-a" /></I18nProvider>);

    await screen.findByText("Launch coffee");
    expect(screen.getByText("Reels")).toBeInTheDocument();
    expect(screen.getByText("0 заявок")).toBeInTheDocument();
    expect(document.querySelector(".campaign-management-screen")).toBeInTheDocument();
    expect(screen.queryByRole("link", { name: translate("myCampaignDetails.backAria", undefined, "ru") })).not.toBeInTheDocument();
    expect(screen.getByRole("link", { name: translate("myCampaignDetails.editAria", { title: "Coffee launch" }, "ru") })).toHaveAttribute("href", "#/my-campaign-edit/campaign-a");
    expect(screen.getByRole("button", { name: translate("myCampaignDetails.closeAria", { title: "Coffee launch" }, "ru") })).toBeInTheDocument();
    expect(screen.queryByText(/views|reach/i)).not.toBeInTheDocument();
  });

  it("does not offer lifecycle actions for a closed campaign", async () => {
    api.getMyCampaign.mockResolvedValueOnce({ ...campaign, status: 2, description: "Launch coffee", requirements: [] });
    render(<I18nProvider><MyCampaignDetails id="campaign-a" /></I18nProvider>);

    await screen.findByText("Coffee launch");
    expect(screen.queryByRole("link", { name: /Редактировать кампанию/i })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /Закрыть кампанию/i })).not.toBeInTheDocument();
    expect(screen.getByText(translate("myCampaignDetails.closedHint", undefined, "ru"))).toBeInTheDocument();
  });
});
