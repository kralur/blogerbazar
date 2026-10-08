import { cleanup, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider, translate } from "../src/i18n";

const api = vi.hoisted(() => ({ getBlogger: vi.fn(), getBloggerReviews: vi.fn(), getCurrentPlatformUser: vi.fn(), getPublicContact: vi.fn() }));

vi.mock("../src/api/marketplace", async (importOriginal) => ({
  ...(await importOriginal<typeof import("../src/api/marketplace")>()),
  getBlogger: api.getBlogger,
  getBloggerReviews: api.getBloggerReviews,
  getCurrentPlatformUser: api.getCurrentPlatformUser,
  getPublicContact: api.getPublicContact
}));
vi.mock("../src/telegram/TelegramProvider", () => ({ useTelegram: () => ({ openLink: vi.fn() }) }));
vi.mock("../src/hooks/useProfileDataRefresh", () => ({ useProfileDataRefresh: vi.fn() }));
vi.mock("../src/components/FavoriteButton", () => ({ FavoriteButton: () => null }));
vi.mock("../src/components/ManagementBackLink", () => ({ ManagementBackLink: () => null }));
vi.mock("../src/components/OfferForm", () => ({ OfferForm: () => null }));
vi.mock("../src/components/ui", async (importOriginal) => ({
  ...(await importOriginal<typeof import("../src/components/ui")>()),
  BottomNav: () => null,
  FixedActionBar: ({ children }: { children: React.ReactNode }) => <div>{children}</div>
}));

import { BloggerDetails } from "../src/pages/BloggerDetails";
import { clearPublicDetailCache } from "../src/data/publicDetailCache";

const ru = (key: string) => translate(key, undefined, "ru");
const blogger = {
  id: "blogger-a", name: "Madina", city: "tashkent", categories: ["beauty"], totalFollowers: 52000, reviewsCount: 0, completedDealsCount: 0,
  averageReach: 0, engagementRate: 8.4, storiesPrice: 300000, reelsPrice: 0, postPrice: 0, integrationPrice: 0, barterEnabled: false,
  platforms: [{ id: "p1", type: "instagram", followers: 52000 }], portfolioItems: []
};

function renderDetails() {
  return render(<I18nProvider><BloggerDetails id="blogger-a" /></I18nProvider>);
}

describe("Blogger details", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    clearPublicDetailCache();
    api.getBlogger.mockResolvedValue(blogger);
    api.getBloggerReviews.mockResolvedValue([]);
    api.getPublicContact.mockRejectedValue(new Error("hidden"));
    api.getCurrentPlatformUser.mockResolvedValue({ selectedMarketplaceRole: "Business" });
  });

  afterEach(() => cleanup());

  it("shows only known prices and never a zero price", async () => {
    renderDetails();

    expect(await screen.findByText(ru("card.stories"))).toBeInTheDocument();
    expect(screen.queryByText(ru("card.reels"))).not.toBeInTheDocument();
    expect(screen.queryByText(/^0\s*сум$/)).not.toBeInTheDocument();
    expect(screen.queryByText(ru("details.reach"))).not.toBeInTheDocument();
  });

  it("says prices are negotiable when none are set", async () => {
    api.getBlogger.mockResolvedValue({ ...blogger, storiesPrice: 0 });
    renderDetails();

    expect(await screen.findByText(ru("details.pricesOnRequest"))).toBeInTheDocument();
  });

  it("lists platforms with localized names", async () => {
    renderDetails();

    expect(await screen.findByRole("region", { name: ru("details.platforms") })).toBeInTheDocument();
    expect(screen.getByText("Instagram")).toBeInTheDocument();
    expect(screen.queryByText("instagram")).not.toBeInTheDocument();
  });

  it("shows followers, reach and ER for each platform with the total on top", async () => {
    api.getBlogger.mockResolvedValue({ ...blogger, totalFollowers: 13000, platforms: [
      { id: "p1", type: "instagram", followers: 10000, averageReach: 25000, engagementRate: 5.5 },
      { id: "p2", type: "telegram", followers: 3000, averageReach: null, engagementRate: null }
    ] });
    renderDetails();

    expect(await screen.findByText(ru("details.totalFollowers"))).toBeInTheDocument();
    expect(screen.getByText("Telegram")).toBeInTheDocument();
    expect(screen.getByText(/ER 5,5%/)).toBeInTheDocument();
    expect(screen.queryByText(ru("details.reach"))).not.toBeInTheDocument();
  });

  it("calls a profile without reviews or deals new instead of showing an empty rating", async () => {
    renderDetails();

    expect(await screen.findByText(ru("details.newProfile"))).toBeInTheDocument();
  });

  it("offers collaboration only to the Business role", async () => {
    const { unmount } = renderDetails();
    expect(await screen.findByRole("button", { name: ru("offers.propose") })).toBeInTheDocument();
    unmount();

    api.getCurrentPlatformUser.mockResolvedValue({ selectedMarketplaceRole: "Blogger" });
    renderDetails();
    await screen.findByText("Madina");
    expect(screen.queryByRole("button", { name: ru("offers.propose") })).not.toBeInTheDocument();
    expect(screen.queryByText(ru("details.createCampaign"))).not.toBeInTheDocument();
  });

  it("links each platform to the blogger's account by its handle", async () => {
    api.getBlogger.mockResolvedValue({ ...blogger, platforms: [
      { id: "p1", type: "instagram", url: "https://instagram.com/madina_k", followers: 10000 },
      { id: "p2", type: "telegram", url: "https://user:pass@t.me/evil", followers: 3000 }
    ] });
    renderDetails();

    const link = await screen.findByRole("link", { name: translate("details.openPlatformProfile", { platform: "Instagram", handle: "@madina_k" }, "ru") });
    expect(link).toHaveAttribute("href", "https://instagram.com/madina_k");
    expect(link).toHaveTextContent("@madina_k");
    expect(document.querySelector(".platform-stats__icon")).toBeInTheDocument();
    // The Telegram handle is rebuilt as a plain t.me link, so the hidden credentials never reach the page.
    expect(screen.getByRole("link", { name: /@evil/ })).toHaveAttribute("href", "https://t.me/evil");
  });

  it("shows the latest reviews side by side with a link to all of them", async () => {
    api.getBlogger.mockResolvedValue({ ...blogger, rating: 4.8, reviewsCount: 12 });
    api.getBloggerReviews.mockResolvedValue([{ id: "r1", dealId: "d1", targetType: 0, rating: 5, comment: "Great reel", reviewerName: "Lumi", createdAtUtc: "2026-09-10T00:00:00Z" }]);
    renderDetails();

    expect(await screen.findByText("Great reel")).toBeInTheDocument();
    expect(document.querySelector(".review-carousel")).toBeInTheDocument();
    expect(document.querySelector(".reviews-heading__summary")?.textContent).toBe("★ 4,8 · 12");
    expect(screen.getByRole("link", { name: ru("reviews.showAll") })).toHaveAttribute("href", "#/blogger-reviews/blogger-a");
    expect(api.getBloggerReviews).toHaveBeenCalledWith("blogger-a", { take: 10 });
  });
});
