import { act, cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider, translate } from "../src/i18n";

const api = vi.hoisted(() => ({ getMarketplaceHome: vi.fn() }));
let profileRefresh: (() => void) | undefined;

vi.mock("../src/api/marketplace", async (importOriginal) => ({
  ...(await importOriginal<typeof import("../src/api/marketplace")>()),
  getMarketplaceHome: api.getMarketplaceHome
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
  });

  it("shows the Business hero, real statistics, and the business section order", async () => {
    renderHome("Business");
    await waitForData();

    expect(screen.getByRole("heading", { name: translate("home.businessHeroTitle", undefined, "ru") })).toBeInTheDocument();
    expect(screen.getByText(translate("home.businessEyebrow", undefined, "ru"))).toBeInTheDocument();
    expect(screen.getAllByText(translate("common.appName", undefined, "ru"))).toHaveLength(1);
    expect(screen.getByRole("link", { name: translate("home.findCreator", undefined, "ru") })).toHaveAttribute("href", "#/search");
    expect(screen.getByText("12")).toBeInTheDocument();
    expect(screen.getByText("8")).toBeInTheDocument();
    expect(screen.queryByText("Hidden business")).not.toBeInTheDocument();
    expect(screen.queryByText(translate("home.promotion", undefined, "ru"))).not.toBeInTheDocument();
    expect(screen.queryByText("5600")).not.toBeInTheDocument();
    expect(screen.queryByText("740")).not.toBeInTheDocument();

    const headings = screen.getAllByRole("heading", { level: 2 }).map((heading) => heading.textContent);
    expect(headings).toEqual(expect.arrayContaining([
      translate("home.categories", undefined, "ru"),
      translate("home.promotedBloggers", undefined, "ru"),
      translate("home.topRated", undefined, "ru"),
      translate("home.newBrandFaces", undefined, "ru"),
      translate("home.newBloggers", undefined, "ru"),
      translate("home.statistics", undefined, "ru")
    ]));
    expect(headings.indexOf(translate("home.categories", undefined, "ru"))).toBeLessThan(headings.indexOf(translate("home.promotedBloggers", undefined, "ru")));
  });

  it("uses role-appropriate Blogger and Brand Face actions without offering Brand Face applications", async () => {
    const { rerender } = render(<I18nProvider><Home role="Blogger" /></I18nProvider>);
    await screen.findByText("Promoted campaign");
    expect(screen.getByRole("heading", { name: translate("home.bloggerHeroTitle", undefined, "ru") })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: translate("home.viewCampaigns", undefined, "ru") })).toHaveAttribute("href", "#/campaigns");
    expect(screen.getAllByRole("link", { name: translate("home.viewAllSection", { section: translate("home.promotedCampaigns", undefined, "ru") }, "ru") })[0]).toHaveAttribute("href", "#/campaigns");

    rerender(<I18nProvider><Home role="BrandFace" /></I18nProvider>);
    expect(await screen.findByRole("heading", { name: translate("home.brandFaceHeroTitle", undefined, "ru") })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: translate("home.openProfile", undefined, "ru") })).toHaveAttribute("href", "#/profile");
    expect(screen.getByText(translate("home.brandFaceEyebrow", undefined, "ru"))).toBeInTheDocument();
    expect(screen.getByText(translate("home.brandFaceHeroDescription", undefined, "ru"))).not.toHaveTextContent("без обещаний отклика");
    expect(screen.queryByText(/подать заявку/i)).not.toBeInTheDocument();
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
    expect(await screen.findByRole("heading", { name: translate("home.businessHeroTitle", undefined, "ru") })).toBeInTheDocument();
    expect(screen.getByText(translate("home.businessNoCreatorsTitle", undefined, "ru"))).toBeInTheDocument();
    expect(screen.getAllByText("0")).toHaveLength(4);
    expect(screen.getByRole("navigation", { name: "bottom-nav" })).toBeInTheDocument();
  });

  it("uses truthful, emoji-free Home section headings", async () => {
    renderHome("Blogger");
    await screen.findByText("Promoted campaign");
    expect(screen.getByRole("heading", { name: translate("home.promotedCampaigns", undefined, "ru") })).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: translate("home.categories", undefined, "ru") })).toBeInTheDocument();
    expect(screen.queryByText("Рекомендуемые кампании")).not.toBeInTheDocument();
    expect(screen.queryByText("Популярные категории")).not.toBeInTheDocument();
    expect(screen.queryByText(/[🔥📢⭐🆕📊]/)).not.toBeInTheDocument();
  });

  it("keeps new Brand Face and campaign copy localized in Uzbek", () => {
    expect(translate("home.brandFaceHeroDescription", undefined, "uz")).toContain("mavjud kampaniyalarni");
    expect(translate("home.promotedCampaigns", undefined, "uz")).toContain("Targ‘ib");
    expect(translate("home.brandFaceEyebrow", undefined, "uz")).toBe("Brend-yuz uchun");
  });

  it("shows neutral loading, preserves the header and hero on failure, and retries only the Home request", async () => {
    let rejectFirst!: () => void;
    api.getMarketplaceHome.mockImplementationOnce(() => new Promise((_, reject) => { rejectFirst = reject; })).mockResolvedValueOnce(response);
    const user = userEvent.setup();
    renderHome("Business");
    expect(screen.getAllByTestId("skeleton").length).toBeGreaterThan(0);
    await act(async () => rejectFirst());
    expect(await screen.findByText(translate("home.errorTitle", undefined, "ru"))).toBeInTheDocument();
    expect(screen.getAllByText(translate("common.appName", undefined, "ru"))).toHaveLength(1);
    expect(screen.getByRole("heading", { name: translate("home.businessHeroTitle", undefined, "ru") })).toBeInTheDocument();
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

  it("keeps category links encoded and marks each rail as an accessible region", async () => {
    api.getMarketplaceHome.mockResolvedValue({ ...response, categories: ["beauty"] });
    renderHome("Business");
    await waitForData();
    const category = screen.getByRole("link", { name: translate("home.openCategory", { category: translate("taxonomy.category.beauty", undefined, "ru") }, "ru") });
    expect(category).toHaveAttribute("href", "#/search?category=beauty");
    expect(screen.getByRole("region", { name: translate("home.promotedBloggers", undefined, "ru") })).toBeInTheDocument();
  });
});
