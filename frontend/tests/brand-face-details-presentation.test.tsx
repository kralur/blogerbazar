import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider, translate } from "../src/i18n";

const api = vi.hoisted(() => ({ getBrandFace: vi.fn(), getBrandFaceReviews: vi.fn(), getCurrentPlatformUser: vi.fn(), createOffer: vi.fn(), getMyCampaigns: vi.fn() }));
const telegram = vi.hoisted(() => ({ openLink: vi.fn(), haptic: { success: vi.fn(), selection: vi.fn(), error: vi.fn() }, registerBackButtonHandler: vi.fn(() => vi.fn()), setBackButtonHandler: vi.fn() }));

vi.mock("../src/api/marketplace", async (importOriginal) => ({
  ...(await importOriginal<typeof import("../src/api/marketplace")>()),
  getBrandFace: api.getBrandFace,
  getBrandFaceReviews: api.getBrandFaceReviews,
  getCurrentPlatformUser: api.getCurrentPlatformUser,
  createOffer: api.createOffer,
  getMyCampaigns: api.getMyCampaigns
}));
vi.mock("../src/telegram/TelegramProvider", () => ({ useTelegram: () => telegram }));
vi.mock("../src/components/FavoriteButton", () => ({ FavoriteButton: () => null }));
vi.mock("../src/components/ContactList", () => ({ ContactList: () => null, hasContacts: () => false }));
vi.mock("../src/hooks/useProfileDataRefresh", () => ({ useProfileDataRefresh: vi.fn() }));

import { BrandFaceDetails } from "../src/pages/BrandFaceDetails";

const profile = {
  id: "face-a", name: "Dilnoza", city: "tashkent-city", categories: ["beauty"], languages: ["uz"], collaborationPrice: null, avatarUrl: null, isPromoted: false,
  instagram: "@dilnoza.face", gender: "female", age: 23, formats: ["photoShoot", "ugc"], showreelUrl: "https://instagram.com/reel/abc",
  photoUrls: ["https://cdn.example/1.webp", "https://cdn.example/2.webp"]
};

// QA Q20: a business sees what it chooses by at once: photos, Instagram, gender and age, formats, showreel.
describe("Brand face public page", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    api.getBrandFace.mockResolvedValue(profile);
    api.getBrandFaceReviews.mockResolvedValue({ rating: null, reviewsCount: 0, items: [] });
    api.getCurrentPlatformUser.mockResolvedValue({ selectedMarketplaceRole: "Business" });
    api.getMyCampaigns.mockResolvedValue({ items: [], total: 0, page: 1, pageSize: 20, hasMore: false });
    api.createOffer.mockResolvedValue({ id: "offer-a" });
  });

  // D48: a business offers a brand face cooperation in brand face formats; other roles see no button.
  it("lets a business send an offer in brand face formats", async () => {
    render(<I18nProvider><BrandFaceDetails id="face-a" /></I18nProvider>);
    fireEvent.click(await screen.findByRole("button", { name: translate("offers.propose", undefined, "ru") }));

    expect(screen.getByRole("button", { name: translate("brandFace.format.photoShoot", undefined, "ru") })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: translate("card.reels", undefined, "ru") })).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: translate("brandFace.format.event", undefined, "ru") }));
    fireEvent.change(screen.getByPlaceholderText(translate("offers.messagePlaceholder", undefined, "ru")), { target: { value: "Event in Tashkent" } });
    fireEvent.click(screen.getByRole("button", { name: translate("offers.send", undefined, "ru") }));

    await waitFor(() => expect(api.createOffer).toHaveBeenCalledWith(expect.objectContaining({ brandFaceId: "face-a", format: "event", message: "Event in Tashkent" })));
    expect(api.createOffer.mock.calls[0][0]).not.toHaveProperty("bloggerId");
  });

  it("says a hidden brand face is paused and offers nothing to a business (D50)", async () => {
    api.getBrandFace.mockResolvedValue({ ...profile, isHidden: true });
    render(<I18nProvider><BrandFaceDetails id="face-a" /></I18nProvider>);

    expect(await screen.findByText(translate("public.hiddenNote", undefined, "ru"))).toBeInTheDocument();
    await waitFor(() => expect(api.getCurrentPlatformUser).toHaveBeenCalled());
    expect(screen.queryByRole("button", { name: translate("offers.propose", undefined, "ru") })).not.toBeInTheDocument();
  });

  it.each(["Blogger", "BrandFace"])("shows no offer button for the %s role", async (role) => {
    api.getCurrentPlatformUser.mockResolvedValue({ selectedMarketplaceRole: role });
    render(<I18nProvider><BrandFaceDetails id="face-a" /></I18nProvider>);

    await screen.findByText("Dilnoza", { selector: "h1" });
    await waitFor(() => expect(api.getCurrentPlatformUser).toHaveBeenCalled());
    expect(screen.queryByRole("button", { name: translate("offers.propose", undefined, "ru") })).not.toBeInTheDocument();
  });
  afterEach(() => cleanup());

  it("shows the gallery, identity, formats and opens Instagram and the showreel", async () => {
    render(<I18nProvider><BrandFaceDetails id="face-a" /></I18nProvider>);

    expect(await screen.findByText("Dilnoza", { selector: "h1" })).toBeInTheDocument();
    expect(screen.getByRole("list", { name: translate("brandFace.galleryTitle", undefined, "ru") }).querySelectorAll("img")).toHaveLength(2);
    expect(screen.getByText(new RegExp(`${translate("brandFace.person.female", undefined, "ru")} · 23`))).toBeInTheDocument();
    expect(screen.getByText(translate("brandFace.format.photoShoot", undefined, "ru"))).toBeInTheDocument();
    expect(screen.getByText(translate("brandFace.format.ugc", undefined, "ru"))).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: translate("brandFace.openInstagram", undefined, "ru") }));
    fireEvent.click(screen.getByRole("button", { name: translate("brandFace.watchShowreel", undefined, "ru") }));
    expect(telegram.openLink).toHaveBeenNthCalledWith(1, "https://instagram.com/dilnoza.face");
    expect(telegram.openLink).toHaveBeenNthCalledWith(2, "https://instagram.com/reel/abc");
  });

  it("hides the gallery and buttons for a profile without them", async () => {
    api.getBrandFace.mockResolvedValue({ ...profile, instagram: null, showreelUrl: null, photoUrls: [], formats: [] });
    render(<I18nProvider><BrandFaceDetails id="face-a" /></I18nProvider>);

    await screen.findByText("Dilnoza", { selector: "h1" });
    expect(screen.queryByRole("list", { name: translate("brandFace.galleryTitle", undefined, "ru") })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: translate("brandFace.openInstagram", undefined, "ru") })).not.toBeInTheDocument();
    expect(screen.queryByText(translate("brandFace.formats", undefined, "ru"))).not.toBeInTheDocument();
  });
});
