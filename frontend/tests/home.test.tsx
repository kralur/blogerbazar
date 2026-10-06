import { act, cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider, translate } from "../src/i18n";

const api = vi.hoisted(() => ({ getMarketplaceHome: vi.fn(), getMyDeals: vi.fn(), getMyCampaigns: vi.fn(), getMyOffers: vi.fn() }));
let profileRefresh: (() => void) | undefined;

vi.mock("../src/api/marketplace", async (importOriginal) => ({
  ...(await importOriginal<typeof import("../src/api/marketplace")>()),
  getMarketplaceHome: api.getMarketplaceHome,
  getMyDeals: api.getMyDeals,
  getMyCampaigns: api.getMyCampaigns,
  getMyOffers: api.getMyOffers
}));
vi.mock("../src/hooks/useScrollRestoration", () => ({ useScrollRestoration: vi.fn() }));
vi.mock("../src/hooks/useProfileDataRefresh", () => ({
  useProfileDataRefresh: (refresh: () => void) => { profileRefresh = refresh; }
}));
vi.mock("../src/components/BloggerCard", () => ({
  BloggerCard: ({ blogger, variant }: { blogger: { id: string; name: string }; variant?: string }) => <a data-variant={variant} href={`#/blogger/${blogger.id}`}>{blogger.name}</a>
}));
vi.mock("../src/components/CampaignCard", () => ({
  CampaignCard: ({ campaign, variant }: { campaign: { id: string; title: string }; variant?: string }) => <a data-variant={variant} href={`#/campaign/${campaign.id}`}>{campaign.title}</a>
}));
vi.mock("../src/components/ui", () => ({
  Avatar: ({ name }: { name: string }) => <span>{name}</span>,
  BottomNav: () => <nav aria-label="bottom-nav" />,
  Icon: () => <svg />,
  Skeleton: ({ className }: { className?: string }) => <div className={className} data-testid="skeleton" />
}));

import { Home } from "../src/pages/Home";

const ru = (key: string) => translate(key, undefined, "ru");

const response = {
  promotedBloggers: [{ id: "promoted-blogger", name: "Promoted blogger", city: "tashkent", categories: ["beauty"], totalFollowers: 1000, reviewsCount: 2, completedDealsCount: 1, isPromoted: true }],
  promotedCampaigns: [{ id: "promoted-campaign", title: "Promoted campaign", description: "Description", categories: ["beauty"], isPromoted: true, applicationsCount: 0, createdAtUtc: "2026-01-01T00:00:00Z" }],
  topRatedBloggers: [{ id: "top-blogger", name: "Top blogger", city: "samarkand", categories: ["fashion"], totalFollowers: 2000, reviewsCount: 4, completedDealsCount: 3 }],
  newBloggers: [{ id: "new-blogger", name: "New blogger", city: "bukhara", categories: ["food"], totalFollowers: 3000, reviewsCount: 0, completedDealsCount: 0 }],
  newBrandFaces: [{ id: "brand-face", name: "Brand face", city: "tashkent", languages: ["ru"], categories: ["beauty"], collaborationPrice: 250000, isPromoted: false }],
  popularBusinesses: [{ id: "business", name: "Hidden business", campaignsCount: 10, completedDealsCount: 3 }],
  categories: ["beauty", "food"],
  statistics: { approvedBloggers: 12, companies: 4, activeCampaigns: 6, completedDeals: 8, averageRating: 4.5 }
};

function renderHome(role: "Business" | "Blogger" | "BrandFace" = "Business") {
  return render(<I18nProvider><Home role={role} /></I18nProvider>);
}

async function waitForData() {
  await screen.findByText("Promoted blogger");
}

describe("Home", () => {
  afterEach(() => cleanup());

  beforeEach(() => {
    vi.clearAllMocks();
    profileRefresh = undefined;
    api.getMarketplaceHome.mockResolvedValue(response);
    api.getMyDeals.mockResolvedValue([]);
    api.getMyCampaigns.mockResolvedValue({ items: [], total: 0, page: 1, pageSize: 20, hasMore: false });
    api.getMyOffers.mockResolvedValue([]);
    window.location.hash = "#/";
  });

  it("puts creator search first for Business and drops the hero and marketplace statistics", async () => {
    const user = userEvent.setup();
    renderHome("Business");
    await waitForData();

    expect(screen.getByRole("heading", { name: ru("home.searchCreatorsTitle") })).toBeInTheDocument();
    expect(screen.queryByText(ru("home.statistics"))).not.toBeInTheDocument();
    expect(screen.queryByRole("heading", { name: ru("home.businessHeroTitle") })).not.toBeInTheDocument();
    expect(screen.queryByText("Hidden business")).not.toBeInTheDocument();
    expect(screen.getByRole("link", { name: ru("search.platformInstagram") })).toHaveAttribute("href", "#/search?platform=instagram");

    const headings = screen.getAllByRole("heading", { level: 2 }).map((heading) => heading.textContent);
    expect(headings.indexOf(ru("home.searchCreatorsTitle"))).toBe(0);
    expect(headings).toEqual(expect.arrayContaining([ru("home.promotedBloggers"), ru("home.topRated"), ru("home.newBrandFaces"), ru("home.newBloggers")]));

    await user.type(screen.getByRole("searchbox", { name: ru("home.searchCreatorsTitle") }), "beauty reels");
    await user.click(screen.getByRole("button", { name: ru("home.searchSubmit") }));
    expect(window.location.hash).toBe("#/search?q=beauty%20reels");
  });

  it("lets Blogger and Brand Face search campaigns without a Brand Face application flow", async () => {
    const user = userEvent.setup();
    const { rerender } = render(<I18nProvider><Home role="Blogger" /></I18nProvider>);
    await screen.findByText("Promoted campaign");
    expect(screen.getByRole("heading", { name: ru("home.searchCampaignsTitle") })).toBeInTheDocument();
    expect(screen.queryByRole("link", { name: ru("search.platformInstagram") })).not.toBeInTheDocument();
    expect(screen.getByRole("link", { name: ru("home.openCategory").replace("{category}", ru("taxonomy.category.beauty")) })).toHaveAttribute("href", "#/campaigns?category=beauty");
    expect(screen.getAllByRole("link", { name: translate("home.viewAllSection", { section: ru("home.promotedCampaigns") }, "ru") })[0]).toHaveAttribute("href", "#/campaigns");
    await user.click(screen.getByRole("button", { name: ru("home.searchSubmit") }));
    expect(window.location.hash).toBe("#/campaigns");

    rerender(<I18nProvider><Home role="BrandFace" /></I18nProvider>);
    expect(await screen.findByRole("heading", { name: ru("home.searchCampaignsTitle") })).toBeInTheDocument();
    expect(screen.queryByText(ru("home.howTitle"))).not.toBeInTheDocument();
    expect(screen.queryByText(/подать заявку/i)).not.toBeInTheDocument();
  });

  it("shows Business tasks from real campaign and deal data above the rails", async () => {
    api.getMyCampaigns.mockResolvedValue({ items: [{ id: "campaign-a", title: "Autumn launch", applicationsCount: 7, status: 1 }, { id: "campaign-b", title: "Quiet campaign", applicationsCount: 0, status: 1 }], total: 2, page: 1, pageSize: 20, hasMore: false });
    api.getMyDeals.mockResolvedValue([{ id: "deal-a", status: 0, canReview: false }, { id: "deal-b", status: 1, canReview: true }]);
    renderHome("Business");

    const campaign = await screen.findByRole("link", { name: new RegExp("Autumn launch") });
    expect(campaign).toHaveAttribute("href", "#/my-campaign-applications/campaign-a");
    expect(campaign).toHaveTextContent(translate("home.activityApplications", { count: 7 }, "ru"));
    expect(screen.queryByText("Quiet campaign")).not.toBeInTheDocument();
    expect(screen.getByRole("link", { name: new RegExp(translate("home.activityActiveDeals", { count: 1 }, "ru")) })).toHaveAttribute("href", "#/requests?tab=deals");
    expect(screen.getByText(translate("home.activityReviews", { count: 1 }, "ru"))).toBeInTheDocument();
    expect(screen.queryByText(ru("home.howTitle"))).not.toBeInTheDocument();
  });

  it("shows a Blogger offer that waits for an answer", async () => {
    api.getMyOffers.mockResolvedValue([{ id: "offer-a", counterpartyName: "Lumi Beauty", canRespond: true }, { id: "offer-b", counterpartyName: "Old offer", canRespond: false }]);
    renderHome("Blogger");

    const offer = await screen.findByRole("link", { name: new RegExp(translate("home.activityOfferFrom", { name: "Lumi Beauty" }, "ru")) });
    expect(offer).toHaveAttribute("href", "#/offer/offer-a");
    expect(screen.queryByText(translate("home.activityOfferFrom", { name: "Old offer" }, "ru"))).not.toBeInTheDocument();
  });

  it("explains how it works when there is nothing to do yet and hides tasks when their requests fail", async () => {
    api.getMyDeals.mockRejectedValue(new Error("offline"));
    api.getMyCampaigns.mockRejectedValue(new Error("offline"));
    renderHome("Business");
    await waitForData();

    expect(await screen.findByRole("region", { name: ru("home.howTitle") })).toBeInTheDocument();
    expect(screen.getByText(ru("home.howBusiness1Title"))).toBeInTheDocument();
    expect(screen.queryByRole("region", { name: ru("home.activityTitle") })).not.toBeInTheDocument();
  });

  it("opens the Brand Face rail in the Brand Face catalog", async () => {
    renderHome("Business");
    await waitForData();
    const action = screen.getByRole("link", { name: translate("home.viewAllSection", { section: translate("home.newBrandFaces", undefined, "ru") }, "ru") });
    expect(action).toHaveAttribute("href", "#/search?type=brand-face");
  });

  it("keeps useful UI for a successful but fully empty response", async () => {
    api.getMarketplaceHome.mockResolvedValue({ ...response, promotedBloggers: [], promotedCampaigns: [], topRatedBloggers: [], newBloggers: [], newBrandFaces: [], categories: [], statistics: { approvedBloggers: 0, companies: 0, activeCampaigns: 0, completedDeals: 0, averageRating: null } });
    renderHome("Business");
    expect(await screen.findByText(translate("home.businessNoCreatorsTitle", undefined, "ru"))).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: ru("home.searchCreatorsTitle") })).toBeInTheDocument();
    expect(screen.getByRole("navigation", { name: "bottom-nav" })).toBeInTheDocument();
  });

  it("uses truthful, emoji-free Home section headings", async () => {
    renderHome("Blogger");
    await screen.findByText("Promoted campaign");
    expect(screen.getByRole("heading", { name: translate("home.promotedCampaigns", undefined, "ru") })).toBeInTheDocument();
    expect(screen.queryByText("Рекомендуемые кампании")).not.toBeInTheDocument();
    expect(screen.queryByText("Популярные категории")).not.toBeInTheDocument();
    expect(screen.queryByText(/[🔥📢⭐🆕📊]/)).not.toBeInTheDocument();
  });

  it("keeps new Home copy localized in Uzbek", () => {
    expect(translate("home.promotedCampaigns", undefined, "uz")).toContain("Targ‘ib");
    expect(translate("home.searchCreatorsTitle", undefined, "uz")).toBe("Reklama uchun bloger toping");
    expect(translate("home.activityTitle", undefined, "uz")).not.toBe("home.activityTitle");
  });

  it("shows neutral loading, preserves the header and search on failure, and retries only the Home request", async () => {
    let rejectFirst!: () => void;
    api.getMarketplaceHome.mockImplementationOnce(() => new Promise((_, reject) => { rejectFirst = reject; })).mockResolvedValueOnce(response);
    const user = userEvent.setup();
    renderHome("Business");
    expect(screen.getAllByTestId("skeleton").length).toBeGreaterThan(0);
    await act(async () => rejectFirst());
    expect(await screen.findByText(translate("home.errorTitle", undefined, "ru"))).toBeInTheDocument();
    expect(screen.getAllByText(translate("common.appName", undefined, "ru"))).toHaveLength(1);
    expect(screen.getByRole("heading", { name: ru("home.searchCreatorsTitle") })).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: translate("common.retry", undefined, "ru") }));
    await waitForData();
    expect(api.getMarketplaceHome).toHaveBeenCalledTimes(2);
  });

  it("does not allow a stale Home request to overwrite a fresh profile refresh", async () => {
    let resolveFirst!: (value: typeof response) => void;
    let resolveSecond!: (value: typeof response) => void;
    api.getMarketplaceHome.mockImplementationOnce(() => new Promise<typeof response>((resolve) => { resolveFirst = resolve; })).mockImplementationOnce(() => new Promise<typeof response>((resolve) => { resolveSecond = resolve; }));
    renderHome("Business");
    expect(profileRefresh).toBeDefined();
    act(() => profileRefresh?.());
    await waitFor(() => expect(api.getMarketplaceHome).toHaveBeenCalledTimes(2));
    await act(async () => resolveSecond({ ...response, promotedBloggers: [{ ...response.promotedBloggers[0], name: "Fresh avatar data" }] }));
    expect(await screen.findByText("Fresh avatar data")).toBeInTheDocument();
    await act(async () => resolveFirst(response));
    await waitFor(() => expect(screen.queryByText("Promoted blogger")).not.toBeInTheDocument());
  });

  it("keeps loaded Home content visible when a background refresh fails", async () => {
    api.getMarketplaceHome.mockResolvedValueOnce(response).mockRejectedValueOnce(new Error("offline"));
    renderHome("Business");
    await waitForData();

    act(() => profileRefresh?.());

    expect(await screen.findByRole("status")).toHaveTextContent(translate("common.connectionRetry", undefined, "ru"));
    expect(screen.getByText("Promoted blogger")).toBeInTheDocument();
    expect(screen.queryByTestId("skeleton")).not.toBeInTheDocument();
  });

  it("keeps category links encoded and marks each rail as an accessible region", async () => {
    api.getMarketplaceHome.mockResolvedValue({ ...response, categories: ["beauty"] });
    renderHome("Business");
    await waitForData();
    const category = screen.getByRole("link", { name: translate("home.openCategory", { category: translate("taxonomy.category.beauty", undefined, "ru") }, "ru") });
    expect(category).toHaveAttribute("href", "#/search?category=beauty");
    expect(screen.getByRole("region", { name: translate("home.promotedBloggers", undefined, "ru") })).toBeInTheDocument();
  });
});
