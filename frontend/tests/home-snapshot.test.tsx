import { render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider, translate } from "../src/i18n";
import { clearHomeSnapshot, homeSnapshotMaxAgeMs, readHomeSnapshot, writeHomeSnapshot } from "../src/data/homeSnapshot";

const api = vi.hoisted(() => ({
  getMarketplaceHome: vi.fn(), getMyDeals: vi.fn(), getMyCampaigns: vi.fn(), getMyOffers: vi.fn(), getRecommendations: vi.fn(),
  getMyBloggerProfile: vi.fn(), getMyBusinessProfile: vi.fn(), getMyBrandFaceProfile: vi.fn()
}));
vi.mock("../src/api/marketplace", async (importOriginal) => ({ ...(await importOriginal<typeof import("../src/api/marketplace")>()), ...api }));
vi.mock("../src/hooks/useScrollRestoration", () => ({ useScrollRestoration: vi.fn() }));
vi.mock("../src/components/BloggerCard", () => ({ BloggerCard: ({ blogger }: { blogger: { name: string } }) => <span>{blogger.name}</span> }));
vi.mock("../src/components/BrandFaceCard", () => ({ BrandFaceCard: ({ profile }: { profile: { name: string } }) => <span>{profile.name}</span> }));
vi.mock("../src/components/CampaignCard", () => ({ CampaignCard: ({ campaign }: { campaign: { title: string } }) => <span>{campaign.title}</span> }));
vi.mock("../src/components/ui", () => ({
  Avatar: () => <span data-testid="avatar" />,
  BottomNav: () => <nav aria-label="bottom-nav" />,
  Icon: () => <svg />,
  Skeleton: ({ className }: { className?: string }) => <div className={className} data-testid="skeleton" />
}));

import { Home } from "../src/pages/Home";

const ru = (key: string) => translate(key, undefined, "ru");
const blogger = (id: string, name: string) => ({ id, name, city: "tashkent", categories: [], avatarUrl: null, totalFollowers: 10, reviewsCount: 0, completedDealsCount: 0 });
const homeWith = (name: string) => ({ categories: ["beauty"], promotedBloggers: [blogger("1", name)], topRatedBloggers: [], newBloggers: [], newBrandFaces: [], promotedCampaigns: [] });

describe("home snapshot store", () => {
  afterEach(() => clearHomeSnapshot());

  it("keeps the last answer per role and forgets it after a week or on clear", () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date("2026-11-07T10:00:00Z"));
    writeHomeSnapshot("Blogger", homeWith("Ali"));
    expect(readHomeSnapshot("Blogger")?.promotedBloggers[0].name).toBe("Ali");
    expect(readHomeSnapshot("Business")).toBeNull();
    vi.setSystemTime(new Date(Date.now() + homeSnapshotMaxAgeMs + 1000));
    expect(readHomeSnapshot("Blogger")).toBeNull();
    writeHomeSnapshot("Blogger", homeWith("Ali"));
    clearHomeSnapshot();
    expect(readHomeSnapshot("Blogger")).toBeNull();
    vi.useRealTimers();
  });

  it("ignores broken storage content", () => {
    localStorage.setItem("bloggerbazar.homeSnapshot.Blogger", "{not json");
    expect(readHomeSnapshot("Blogger")).toBeNull();
  });
});

describe("Home with a snapshot", () => {
  beforeEach(() => {
    api.getMyDeals.mockResolvedValue([]);
    api.getMyCampaigns.mockResolvedValue({ items: [] });
    api.getMyOffers.mockResolvedValue([]);
    api.getRecommendations.mockResolvedValue({ audience: "none", campaigns: [], bloggers: [], brandFaces: [] });
    api.getMyBloggerProfile.mockRejectedValue(new Error("no profile"));
    api.getMyBusinessProfile.mockRejectedValue(new Error("no profile"));
    api.getMyBrandFaceProfile.mockRejectedValue(new Error("no profile"));
  });
  afterEach(() => { vi.clearAllMocks(); clearHomeSnapshot(); });

  it("shows the remembered Home at once, then the fresh answer, and remembers the fresh answer", async () => {
    writeHomeSnapshot("Business", homeWith("Ali"));
    let resolveHome: (value: unknown) => void = () => undefined;
    api.getMarketplaceHome.mockReturnValue(new Promise((resolve) => { resolveHome = resolve; }));
    render(<I18nProvider><Home role="Business" /></I18nProvider>);
    expect(screen.getByText("Ali")).toBeInTheDocument();
    expect(screen.queryByTestId("skeleton")).toBeNull();
    resolveHome(homeWith("Bekzod"));
    await screen.findByText("Bekzod");
    expect(screen.queryByText("Ali")).toBeNull();
    await waitFor(() => expect(readHomeSnapshot("Business")?.promotedBloggers[0].name).toBe("Bekzod"));
  });

  it("keeps the remembered Home and says the refresh failed when the network is down", async () => {
    writeHomeSnapshot("Business", homeWith("Ali"));
    api.getMarketplaceHome.mockRejectedValue(new Error("offline"));
    render(<I18nProvider><Home role="Business" /></I18nProvider>);
    await screen.findByText(ru("common.connectionRetry"));
    expect(screen.getByText("Ali")).toBeInTheDocument();
    expect(readHomeSnapshot("Business")?.promotedBloggers[0].name).toBe("Ali");
  });

  it("starts from the skeleton without a snapshot", () => {
    api.getMarketplaceHome.mockReturnValue(new Promise(() => undefined));
    render(<I18nProvider><Home role="Business" /></I18nProvider>);
    expect(screen.getAllByTestId("skeleton").length).toBeGreaterThan(0);
  });
});
