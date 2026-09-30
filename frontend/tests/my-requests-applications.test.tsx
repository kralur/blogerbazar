import { fireEvent, render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider, translate } from "../src/i18n";

const api = vi.hoisted(() => ({ getMyCampaignApplications: vi.fn(), getMyDeals: vi.fn() }));

vi.mock("../src/api/marketplace", async (importOriginal) => ({
  ...(await importOriginal<typeof import("../src/api/marketplace")>()),
  getMyCampaignApplications: api.getMyCampaignApplications,
  getMyDeals: api.getMyDeals
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
});
