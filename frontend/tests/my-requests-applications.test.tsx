import { fireEvent, render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider, translate } from "../src/i18n";

const api = vi.hoisted(() => ({ getMyCampaignApplications: vi.fn(), getMyDeals: vi.fn(), getMyOffers: vi.fn() }));

vi.mock("../src/api/marketplace", async (importOriginal) => ({
  ...(await importOriginal<typeof import("../src/api/marketplace")>()),
  getMyCampaignApplications: api.getMyCampaignApplications,
  getMyDeals: api.getMyDeals,
  getMyOffers: api.getMyOffers
}));
vi.mock("../src/pages/BloggerApplications", () => ({ BloggerApplications: () => <section>blogger-applications</section> }));
vi.mock("../src/components/LanguageSwitcher", () => ({ LanguageSwitcher: () => null }));
vi.mock("../src/hooks/useProfileDataRefresh", () => ({ useProfileDataRefresh: () => undefined }));
vi.mock("../src/hooks/useScrollRestoration", () => ({ useScrollRestoration: () => undefined }));
vi.mock("../src/components/ui", () => ({
  Avatar: () => null,
  Badge: ({ children }: { children: React.ReactNode }) => <span>{children}</span>,
  BottomNav: () => null,
  BottomSheet: () => null,
  Button: ({ children, ...props }: React.ButtonHTMLAttributes<HTMLButtonElement>) => <button {...props}>{children}</button>,
  Card: ({ children }: { children: React.ReactNode }) => <section>{children}</section>,
  EmptyState: ({ title }: { title: string }) => <p>{title}</p>,
  ErrorState: ({ title }: { title: string }) => <p>{title}</p>,
  Icon: () => null,
  Input: () => null,
  LoadingState: ({ title }: { title: string }) => <p>{title}</p>,
  Modal: () => null,
  Textarea: () => null,
  Toast: () => null
}));

import { MyRequests } from "../src/pages/MyRequests";

describe("My Requests role-aware loading", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    api.getMyCampaignApplications.mockResolvedValue([]);
    api.getMyOffers.mockResolvedValue([]);
  });

  it("keeps Blogger applications available when Deals fails", async () => {
    api.getMyDeals.mockRejectedValueOnce(new Error("deals unavailable"));
    render(<I18nProvider><MyRequests activeMarketplaceRole="Blogger" /></I18nProvider>);

    expect(await screen.findByText("blogger-applications")).toBeInTheDocument();
    expect(api.getMyCampaignApplications).not.toHaveBeenCalled();
  });

  it("preserves the existing Deals flow independently", async () => {
    api.getMyDeals.mockResolvedValueOnce([{ id: "deal-a", title: "Coffee", counterpartyName: "Lumi", status: 0, createdAtUtc: "2026-09-01T00:00:00Z", canComplete: false, canReview: false }]);
    render(<I18nProvider><MyRequests activeMarketplaceRole="Blogger" /></I18nProvider>);
    await screen.findByText("blogger-applications");
    fireEvent.click(screen.getByRole("button", { name: translate("requests.deals", undefined, "ru") }));
    expect(await screen.findByText("Coffee")).toBeInTheDocument();
  });

  it("marks the selected tab for assistive technology", async () => {
    api.getMyDeals.mockResolvedValueOnce([]);
    render(<I18nProvider><MyRequests activeMarketplaceRole="Blogger" /></I18nProvider>);
    await screen.findByText("blogger-applications");
    const deals = screen.getByRole("button", { name: translate("requests.deals", undefined, "ru") });
    expect(screen.getByRole("button", { name: translate("requests.applications", undefined, "ru") })).toHaveAttribute("aria-pressed", "true");
    fireEvent.click(deals);
    expect(deals).toHaveAttribute("aria-pressed", "true");
  });

  it("opens the tab named in a Home link", async () => {
    api.getMyDeals.mockResolvedValueOnce([{ id: "deal-a", title: "Coffee", counterpartyName: "Lumi", status: 0, createdAtUtc: "2026-09-01T00:00:00Z", canComplete: true, canReview: false }]);
    window.location.hash = "#/requests?tab=deals";
    render(<I18nProvider><MyRequests activeMarketplaceRole="Business" /></I18nProvider>);

    expect(await screen.findByText("Coffee")).toBeInTheDocument();
    window.location.hash = "#/requests";
  });

  it("links Deal cards to the deal route and translates collaboration deals", async () => {
    api.getMyDeals.mockResolvedValueOnce([
      { id: "deal-a", title: "Coffee", counterpartyName: "Lumi", status: 0, createdAtUtc: "2026-09-01T00:00:00Z", canComplete: true, canReview: false, sourceType: "campaignApplication", termsSource: "snapshot" },
      { id: "deal-b", title: "Direct collaboration request", counterpartyName: "Ali", status: 1, createdAtUtc: "2026-09-02T00:00:00Z", canComplete: false, canReview: true, sourceType: "collaborationRequest", termsSource: "collaboration" }
    ]);
    render(<I18nProvider><MyRequests activeMarketplaceRole="Business" /></I18nProvider>);
    fireEvent.click(screen.getByRole("button", { name: translate("requests.deals", undefined, "ru") }));

    expect((await screen.findByText("Coffee")).closest("a")).toHaveAttribute("href", "#/deal/deal-a");
    expect(screen.getByText(translate("deals.source.collaborationRequest", undefined, "ru")).closest("a")).toHaveAttribute("href", "#/deal/deal-b");
    expect(screen.queryByText("Direct collaboration request")).not.toBeInTheDocument();
  });

  it("lists offers and links them to the offer route", async () => {
    api.getMyDeals.mockResolvedValueOnce([]);
    api.getMyOffers.mockResolvedValueOnce([{ id: "offer-a", bloggerId: "blogger-a", counterpartyName: "Lumi", format: "reels", message: "Hi", state: "pending", createdAtUtc: "2026-10-05T00:00:00Z", canRespond: true }]);
    render(<I18nProvider><MyRequests activeMarketplaceRole="Blogger" /></I18nProvider>);
    fireEvent.click(screen.getByRole("button", { name: translate("offers.tab", undefined, "ru") }));

    expect((await screen.findByText("Lumi")).closest("a")).toHaveAttribute("href", "#/offer/offer-a");
    expect(screen.getByText(translate("offers.state.pending", undefined, "ru"))).toBeInTheDocument();
  });
});
