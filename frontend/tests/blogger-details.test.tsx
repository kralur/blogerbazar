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

    expect(await screen.findByText(ru("details.platforms"))).toBeInTheDocument();
    expect(screen.getByText("Instagram")).toBeInTheDocument();
    expect(screen.queryByText("instagram")).not.toBeInTheDocument();
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
});
