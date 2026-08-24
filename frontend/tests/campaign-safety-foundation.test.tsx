import { cleanup, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider, translate } from "../src/i18n";
import { CampaignApplicationStatus, canAcceptCampaignApplication, campaignApplicationStatusTone } from "../src/lib/campaignApplicationStatus";

const api = vi.hoisted(() => ({
  applyToCampaign: vi.fn(),
  getCampaign: vi.fn(),
  getCurrentPlatformUser: vi.fn(),
  getMyBloggerProfile: vi.fn(),
  getMyBusinessProfile: vi.fn(),
  getPublicContact: vi.fn()
}));

vi.mock("../src/api/marketplace", async (importOriginal) => ({
  ...(await importOriginal<typeof import("../src/api/marketplace")>()),
  applyToCampaign: api.applyToCampaign,
  getCampaign: api.getCampaign,
  getCurrentPlatformUser: api.getCurrentPlatformUser,
  getMyBloggerProfile: api.getMyBloggerProfile,
  getMyBusinessProfile: api.getMyBusinessProfile,
  getPublicContact: api.getPublicContact
}));
vi.mock("../src/components/LanguageSwitcher", () => ({ LanguageSwitcher: () => <span>language</span> }));
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
    api.getMyBusinessProfile.mockRejectedValue(new Error("no business profile"));
    api.getMyBloggerProfile.mockResolvedValue({ id: "blogger-a", status: 1 });
    api.getCurrentPlatformUser.mockResolvedValue({ selectedMarketplaceRole: "Blogger" });
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
});
