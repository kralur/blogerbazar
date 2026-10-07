import { render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider, translate } from "../src/i18n";
import { BusinessDetails } from "../src/pages/BusinessDetails";

const api = vi.hoisted(() => ({ getPublicBusiness: vi.fn() }));
vi.mock("../src/api/marketplace", () => api);
vi.mock("../src/telegram/TelegramProvider", () => ({ useTelegram: () => ({ haptic: { selection: vi.fn(), success: vi.fn(), error: vi.fn() }, openLink: vi.fn(), setBackButtonHandler: vi.fn(), registerBackButtonHandler: () => () => undefined }) }));

const ru = (key: string, values?: Record<string, string | number>) => translate(key, values, "ru");

describe("business profile page", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    window.sessionStorage.clear();
  });

  it("shows open campaigns, reviews linked to bloggers and no private contacts", async () => {
    api.getPublicBusiness.mockResolvedValue({
      id: "biz-1", name: "Lumi Beauty", city: "tashkent", logoUrl: null, websiteUrl: null, description: "Косметика", isVerified: true,
      completedDealsCount: 3, createdAtUtc: "2026-09-01T00:00:00Z", rating: 4.5, reviewsCount: 2,
      openCampaigns: [{ id: "camp-1", title: "Запуск ресторана", city: null, budgetFrom: 500000, budgetTo: 2000000, deadline: null }],
      reviews: [{ id: "r1", rating: 5, comment: "Хороший", reviewerName: "Madina", createdAtUtc: "2026-10-01T00:00:00Z", reviewerProfileId: "blog-9", reviewerImageUrl: null }]
    });
    render(<I18nProvider><BusinessDetails id="biz-1" /></I18nProvider>);

    expect(await screen.findByRole("heading", { name: "Lumi Beauty" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: /Запуск ресторана/ })).toHaveAttribute("href", "#/campaign/camp-1");
    expect(screen.getByRole("link", { name: /Madina/ })).toHaveAttribute("href", "#/blogger/blog-9");
    expect(screen.getByText(ru("company.completedDeals"))).toBeInTheDocument();
    expect(screen.queryByText(ru("contacts.phone"))).not.toBeInTheDocument();
  });

  it("says when there are no open campaigns", async () => {
    api.getPublicBusiness.mockResolvedValue({ id: "biz-2", name: "Brew Lab", isVerified: false, completedDealsCount: 0, createdAtUtc: "2026-09-01T00:00:00Z", reviewsCount: 0, openCampaigns: [], reviews: [] });
    render(<I18nProvider><BusinessDetails id="biz-2" /></I18nProvider>);

    expect(await screen.findByText(ru("company.noOpenCampaigns"))).toBeInTheDocument();
  });
});
