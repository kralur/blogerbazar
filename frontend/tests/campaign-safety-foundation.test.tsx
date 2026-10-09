import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider, translate } from "../src/i18n";
import { CampaignApplicationStatus, canAcceptCampaignApplication, campaignApplicationStatusTone } from "../src/lib/campaignApplicationStatus";

const api = vi.hoisted(() => ({
  applyToCampaign: vi.fn(),
  getCampaign: vi.fn(),
  getCurrentPlatformUser: vi.fn(),
  getMyBloggerProfile: vi.fn(),
  getMyCampaignApplicationsPage: vi.fn(),
  getMyBusinessProfile: vi.fn(),
  getMyBrandFaceProfile: vi.fn(),
  getPublicContact: vi.fn(),
  getBusinessReviews: vi.fn()
}));

vi.mock("../src/api/marketplace", async (importOriginal) => ({
  ...(await importOriginal<typeof import("../src/api/marketplace")>()),
  applyToCampaign: api.applyToCampaign,
  getCampaign: api.getCampaign,
  getCurrentPlatformUser: api.getCurrentPlatformUser,
  getMyBloggerProfile: api.getMyBloggerProfile,
  getMyCampaignApplicationsPage: api.getMyCampaignApplicationsPage,
  getMyBusinessProfile: api.getMyBusinessProfile,
  getMyBrandFaceProfile: api.getMyBrandFaceProfile,
  getPublicContact: api.getPublicContact,
  getBusinessReviews: api.getBusinessReviews
}));
vi.mock("../src/components/ManagementBackLink", () => ({ ManagementBackLink: () => null }));
vi.mock("../src/components/ContactList", () => ({ ContactList: () => null, hasContacts: () => false }));
vi.mock("../src/components/ui", () => ({
  Avatar: ({ name }: { name: string }) => <span>{name}</span>,
  Badge: ({ children }: { children: React.ReactNode }) => <span>{children}</span>,
  BottomNav: () => <nav aria-label="bottom-nav" />,
  Button: ({ children, ...props }: React.ButtonHTMLAttributes<HTMLButtonElement>) => <button {...props}>{children}</button>,
  Card: ({ children }: { children: React.ReactNode }) => <section>{children}</section>,
  ErrorState: () => <div>error</div>,
  FixedActionBar: ({ children }: { children: React.ReactNode }) => <div>{children}</div>,
  Icon: () => <svg />,
  LoadingState: () => <div>loading</div>,
  Modal: ({ children, open }: { children: React.ReactNode; open: boolean }) => open ? <div>{children}</div> : null,
  Rating: ({ value, count }: { value?: number | null; count?: number }) => <span>{`rating ${value ?? "-"}${count === undefined ? "" : ` of ${count}`}`}</span>,
  Textarea: () => <textarea />,
  Toast: () => null
}));

import { CampaignDetails } from "../src/pages/CampaignDetails";

const campaign = {
  id: "campaign-a",
  businessId: "business-a",
  company: "Lumi Beauty",
  title: "Campaign",
  description: "Description",
  categories: ["beauty"],
  requirements: [],
  isPromoted: false,
  status: 1,
  applicationsCount: 0,
  budgetFrom: null,
  budgetTo: null,
  city: null
};

function renderDetails() {
  return render(<I18nProvider><CampaignDetails id="campaign-a" /></I18nProvider>);
}

describe("Campaign safety foundation", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    api.getCampaign.mockResolvedValue(campaign);
    api.getPublicContact.mockResolvedValue({});
    api.getBusinessReviews.mockResolvedValue({ rating: null, reviewsCount: 0, items: [] });
    api.getMyBusinessProfile.mockRejectedValue(new Error("no business profile"));
    api.getMyBrandFaceProfile.mockRejectedValue(new Error("no brand face profile"));
    api.getMyBloggerProfile.mockResolvedValue({ id: "blogger-a", status: 1 });
    api.getCurrentPlatformUser.mockResolvedValue({ selectedMarketplaceRole: "Blogger" });
    api.getMyCampaignApplicationsPage.mockResolvedValue({ items: [], total: 0, page: 1, pageSize: 1, hasMore: false });
  });

  afterEach(() => cleanup());

  it("keeps frontend numeric application statuses identical to the backend enum", () => {
    expect(CampaignApplicationStatus).toEqual({ Sent: 0, Viewed: 1, Accepted: 2, Rejected: 3, Withdrawn: 4 });
    expect(canAcceptCampaignApplication(CampaignApplicationStatus.Sent)).toBe(true);
    expect(canAcceptCampaignApplication(CampaignApplicationStatus.Viewed)).toBe(true);
    expect(canAcceptCampaignApplication(CampaignApplicationStatus.Accepted)).toBe(false);
    expect(canAcceptCampaignApplication(CampaignApplicationStatus.Rejected)).toBe(false);
    expect(canAcceptCampaignApplication(CampaignApplicationStatus.Withdrawn)).toBe(false);
    expect(campaignApplicationStatusTone(CampaignApplicationStatus.Accepted)).toBe("green");
    expect(campaignApplicationStatusTone(CampaignApplicationStatus.Rejected)).toBe("red");
  });

  it("has localized labels for every backend application status", () => {
    for (const language of ["ru", "uz"] as const) {
      expect(translate("requests.applicationSent", undefined, language)).not.toBe("requests.applicationSent");
      expect(translate("requests.applicationViewed", undefined, language)).not.toBe("requests.applicationViewed");
      expect(translate("requests.applicationAccepted", undefined, language)).not.toBe("requests.applicationAccepted");
      expect(translate("requests.applicationRejected", undefined, language)).not.toBe("requests.applicationRejected");
      expect(translate("requests.applicationWithdrawn", undefined, language)).not.toBe("requests.applicationWithdrawn");
    }
  });

  it("shows apply only for an active approved Blogger profile", async () => {
    renderDetails();

    expect(await screen.findByText("Campaign")).toBeInTheDocument();
    await waitFor(() => expect(screen.getByRole("button", { name: translate("campaign.apply", undefined, "ru") })).toBeInTheDocument());
  });

  it("closes applications once the campaign deadline day has passed", async () => {
    api.getCampaign.mockResolvedValue({ ...campaign, deadline: "2020-01-01T00:00:00Z" });
    renderDetails();

    expect(await screen.findByRole("status")).toHaveTextContent(translate("error.campaign_expired", undefined, "ru"));
    expect(screen.getByText(translate("campaign.expired", undefined, "ru"))).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: translate("campaign.apply", undefined, "ru") })).not.toBeInTheDocument();
  });

  it.each([
    ["Business", 1],
    ["BrandFace", 1],
    ["Blogger", 0]
  ] as const)("hides apply for %s when the real capability is unavailable", async (role, bloggerStatus) => {
    api.getCurrentPlatformUser.mockResolvedValue({ selectedMarketplaceRole: role });
    api.getMyBloggerProfile.mockResolvedValue({ id: "blogger-a", status: bloggerStatus });
    renderDetails();

    await screen.findByText("Campaign");
    await waitFor(() => expect(screen.queryByRole("button", { name: translate("campaign.apply", undefined, "ru") })).not.toBeInTheDocument());
  });

  it("does not render fake budget, city, or date values for missing API fields", async () => {
    renderDetails();

    await screen.findByText("Campaign");
    expect(screen.queryByText(/0\s*сум/)).not.toBeInTheDocument();
    expect(screen.queryByText(translate("taxonomy.city.uzbekistan", undefined, "ru"))).not.toBeInTheDocument();
  });

  it.each([CampaignApplicationStatus.Rejected, CampaignApplicationStatus.Withdrawn])("never restores Apply when an existing final application has status %s", async (status) => {
    api.getMyCampaignApplicationsPage.mockResolvedValueOnce({ items: [{ id: "application-a", status }], total: 1, page: 1, pageSize: 1, hasMore: false });
    renderDetails();

    await screen.findByText("Campaign");
    await waitFor(() => expect(screen.getByRole("link", { name: translate("applications.applyState", undefined, "ru") })).toHaveAttribute("href", "#/my-application/application-a"));
    expect(screen.queryByRole("button", { name: translate("campaign.apply", undefined, "ru") })).not.toBeInTheDocument();
  });

  it("ignores a stale lookup from campaign A after navigation to campaign B", async () => {
    let resolveCampaignALookup!: (value: { items: Array<{ id: string; status: CampaignApplicationStatus }>; total: number; page: number; pageSize: number; hasMore: boolean }) => void;
    api.getCampaign.mockImplementation((campaignId: string) => Promise.resolve({ ...campaign, id: campaignId, title: campaignId === "campaign-b" ? "Campaign B" : "Campaign A" }));
    api.getMyCampaignApplicationsPage.mockImplementation(({ campaignId }: { campaignId: string }) => campaignId === "campaign-a"
      ? new Promise((resolve) => { resolveCampaignALookup = resolve; })
      : Promise.resolve({ items: [], total: 0, page: 1, pageSize: 1, hasMore: false }));
    const view = render(<I18nProvider><CampaignDetails id="campaign-a" /></I18nProvider>);
    await screen.findByText("Campaign A");
    view.rerender(<I18nProvider><CampaignDetails id="campaign-b" /></I18nProvider>);
    await screen.findByText("Campaign B");
    resolveCampaignALookup({ items: [{ id: "application-a", status: CampaignApplicationStatus.Sent }], total: 1, page: 1, pageSize: 1, hasMore: false });
    await waitFor(() => expect(screen.queryByRole("link", { name: translate("applications.applyState", undefined, "ru") })).not.toBeInTheDocument());
    expect(screen.getByRole("button", { name: translate("campaign.apply", undefined, "ru") })).toBeInTheDocument();
  });

  it("does not apply a stale campaign A mutation after navigation to campaign B", async () => {
    let resolveApply!: (value: { id: string; status: CampaignApplicationStatus }) => void;
    api.getCampaign.mockImplementation((campaignId: string) => Promise.resolve({ ...campaign, id: campaignId, title: campaignId === "campaign-b" ? "Campaign B" : "Campaign A" }));
    api.applyToCampaign.mockImplementationOnce(() => new Promise((resolve) => { resolveApply = resolve; }));
    const view = render(<I18nProvider><CampaignDetails id="campaign-a" /></I18nProvider>);
    await screen.findByText("Campaign A");
    await screen.findByRole("button", { name: translate("campaign.apply", undefined, "ru") });
    fireEvent.click(screen.getByRole("button", { name: translate("campaign.apply", undefined, "ru") }));
    fireEvent.click(screen.getByRole("button", { name: translate("campaign.submitApplication", undefined, "ru") }));
    view.rerender(<I18nProvider><CampaignDetails id="campaign-b" /></I18nProvider>);
    await screen.findByText("Campaign B");
    resolveApply({ id: "application-a", status: CampaignApplicationStatus.Sent });
    await waitFor(() => expect(screen.queryByRole("link", { name: translate("applications.applyState", undefined, "ru") })).not.toBeInTheDocument());
    expect(screen.getByRole("button", { name: translate("campaign.apply", undefined, "ru") })).toBeInTheDocument();
  });
  it("lets a brand face with a profile apply like a blogger (D46)", async () => {
    api.getCurrentPlatformUser.mockResolvedValue({ selectedMarketplaceRole: "BrandFace" });
    api.getMyBloggerProfile.mockRejectedValue(new Error("no blogger profile"));
    api.getMyBrandFaceProfile.mockResolvedValue({ id: "brand-face-a", name: "Dilnoza" });
    renderDetails();

    expect(await screen.findByRole("button", { name: translate("campaign.apply", undefined, "ru") })).toBeInTheDocument();
    expect(api.getMyCampaignApplicationsPage).toHaveBeenCalled();
  });

  it("asks a brand face without a profile to fill it in instead of sending it to switch roles", async () => {
    api.getCurrentPlatformUser.mockResolvedValue({ selectedMarketplaceRole: "BrandFace" });
    renderDetails();

    expect(await screen.findByText(translate("campaign.blockedNoBrandFaceProfile", undefined, "ru"))).toBeInTheDocument();
    expect(screen.getByRole("link", { name: translate("campaign.blockedNoProfileAction", undefined, "ru") })).toHaveAttribute("href", "#/brand-face");
    expect(screen.queryByRole("button", { name: translate("campaign.apply", undefined, "ru") })).not.toBeInTheDocument();
    expect(screen.queryByText(translate("campaign.blockedRole", undefined, "ru"))).not.toBeInTheDocument();
  });

  it("shows the business rating and published reviews", async () => {
    api.getBusinessReviews.mockResolvedValue({
      rating: 4.5,
      reviewsCount: 2,
      items: [{ id: "review-a", rating: 5, comment: "Clear brief, fast payment", reviewerName: "Madina", createdAtUtc: "2026-09-10T00:00:00Z" }]
    });
    renderDetails();

    expect(await screen.findByText(translate("campaign.businessReviews", undefined, "ru"))).toBeInTheDocument();
    expect(document.querySelector(".reviews-heading__summary")?.textContent).toBe("★ 4,5 · 2");
    expect(screen.getByRole("link", { name: translate("reviews.showAll", undefined, "ru") })).toHaveAttribute("href", "#/company-reviews/business-a");
    expect(screen.getByText("Clear brief, fast payment")).toBeInTheDocument();
    expect(api.getBusinessReviews).toHaveBeenCalledWith("business-a", expect.any(AbortSignal));
  });

  it("says when the business has no reviews yet", async () => {
    renderDetails();

    expect(await screen.findByText(translate("campaign.noBusinessReviews", undefined, "ru"))).toBeInTheDocument();
    expect(document.querySelector(".reviews-heading__summary")).toBeNull();
    expect(screen.queryByRole("link", { name: translate("reviews.showAll", undefined, "ru") })).not.toBeInTheDocument();
  });
});
