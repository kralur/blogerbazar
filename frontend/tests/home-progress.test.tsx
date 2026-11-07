import { render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider, translate } from "../src/i18n";

const api = vi.hoisted(() => ({
  getMarketplaceHome: vi.fn(), getMyDeals: vi.fn(), getMyCampaigns: vi.fn(), getMyOffers: vi.fn(), getRecommendations: vi.fn(),
  getMyBloggerProfile: vi.fn(), getMyBusinessProfile: vi.fn(), getMyBrandFaceProfile: vi.fn()
}));

vi.mock("../src/api/marketplace", async (importOriginal) => ({
  ...(await importOriginal<typeof import("../src/api/marketplace")>()),
  ...api
}));
vi.mock("../src/hooks/useScrollRestoration", () => ({ useScrollRestoration: vi.fn() }));
vi.mock("../src/components/BloggerCard", () => ({ BloggerCard: ({ blogger }: { blogger: { name: string } }) => <span>{blogger.name}</span> }));
vi.mock("../src/components/BrandFaceCard", () => ({ BrandFaceCard: ({ profile }: { profile: { name: string } }) => <span>{profile.name}</span> }));
vi.mock("../src/components/CampaignCard", () => ({ CampaignCard: ({ campaign }: { campaign: { title: string } }) => <span>{campaign.title}</span> }));
vi.mock("../src/components/ui", () => ({
  Avatar: ({ name }: { name: string }) => <span>{name}</span>,
  BottomNav: () => <nav aria-label="bottom-nav" />,
  Icon: () => <svg />,
  Skeleton: ({ className }: { className?: string }) => <div className={className} data-testid="skeleton" />
}));

import { Home } from "../src/pages/Home";

const ru = (key: string, values?: Record<string, string | number>) => translate(key, values, "ru");
const emptyHome = { categories: [], promotedBloggers: [], topRatedBloggers: [], newBloggers: [], newBrandFaces: [], promotedCampaigns: [] };
const blogger = { id: "b1", name: "Ali", city: "tashkent", categories: ["beauty"], totalFollowers: 100, barterEnabled: false, status: 1, portfolioItems: [], platforms: [] };

describe("Home profile progress", () => {
  beforeEach(() => {
    api.getMarketplaceHome.mockResolvedValue(emptyHome);
    api.getMyDeals.mockResolvedValue([]);
    api.getMyCampaigns.mockResolvedValue({ items: [] });
    api.getMyOffers.mockResolvedValue([]);
    api.getRecommendations.mockResolvedValue({ audience: "none", campaigns: [], bloggers: [], brandFaces: [] });
    api.getMyBloggerProfile.mockResolvedValue({ ...blogger, avatarUrl: "https://cdn/a.webp" });
    api.getMyBusinessProfile.mockResolvedValue({ id: "c1", name: "Cafe", isVerified: false, moderationStatus: 1 });
    api.getMyBrandFaceProfile.mockRejectedValue(new Error("no profile"));
  });
  afterEach(() => vi.clearAllMocks());

  it("shows a blogger how full the profile is and the next step with a link to the form", async () => {
    render(<I18nProvider><Home role="Blogger" /></I18nProvider>);
    const progress = await screen.findByRole("region", { name: ru("home.progress.title", { percent: 20 }) });
    expect(progress).toHaveTextContent(ru("home.progress.next", { step: ru("home.progress.about") }));
    expect(screen.getByRole("link", { name: ru("home.progress.action") })).toHaveAttribute("href", "#/blogger-form");
    expect(screen.getByRole("progressbar")).toHaveAttribute("aria-valuenow", "20");
    expect(api.getMyBusinessProfile).not.toHaveBeenCalled();
  });

  it("asks a business for its own profile and links to the company form", async () => {
    render(<I18nProvider><Home role="Business" /></I18nProvider>);
    await screen.findByRole("region", { name: ru("home.progress.title", { percent: 0 }) });
    expect(screen.getByRole("link", { name: ru("home.progress.action") })).toHaveAttribute("href", "#/business");
    expect(api.getMyBloggerProfile).not.toHaveBeenCalled();
  });

  it("hides the bar for a complete profile and when the profile cannot be read", async () => {
    api.getMyBloggerProfile.mockResolvedValue({ ...blogger, avatarUrl: "a", bio: "b", postPrice: 10, portfolioItems: [{ id: "i", title: "t", type: 0, url: "u" }], platforms: [{ id: "1", type: "instagram", url: "u" }, { id: "2", type: "telegram", url: "u" }] });
    const { unmount } = render(<I18nProvider><Home role="Blogger" /></I18nProvider>);
    await waitFor(() => expect(api.getMyBloggerProfile).toHaveBeenCalled());
    await screen.findByRole("heading", { name: ru("home.howTitle") });
    expect(screen.queryByRole("progressbar")).toBeNull();
    unmount();

    api.getMyBloggerProfile.mockRejectedValue(new Error("offline"));
    render(<I18nProvider><Home role="Blogger" /></I18nProvider>);
    await screen.findByRole("heading", { name: ru("home.howTitle") });
    expect(screen.queryByRole("progressbar")).toBeNull();
  });

  it("names every step in Uzbek too", () => {
    for (const step of ["photo", "about", "portfolio", "platforms", "prices", "logo", "website", "email", "telegram", "gallery", "showreel", "price"]) {
      expect(translate(`home.progress.${step}`, undefined, "uz")).not.toBe(`home.progress.${step}`);
    }
  });
});
