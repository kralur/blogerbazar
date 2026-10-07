import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { ApiError } from "../src/api/client";
import { I18nProvider, translate } from "../src/i18n";

const api = vi.hoisted(() => ({ getMyDeal: vi.fn(), getDealContact: vi.fn(), completeDeal: vi.fn(), createDealReview: vi.fn() }));

vi.mock("../src/api/marketplace", async (importOriginal) => ({
  ...(await importOriginal<typeof import("../src/api/marketplace")>()),
  getMyDeal: api.getMyDeal,
  getDealContact: api.getDealContact,
  completeDeal: api.completeDeal,
  createDealReview: api.createDealReview
}));
vi.mock("../src/components/ManagementBackLink", () => ({ ManagementBackLink: () => null }));
vi.mock("../src/components/ContactList", () => ({
  hasContacts: (items: Array<{ value: string }>) => items.some((item) => item.value),
  ContactList: ({ items }: { items: Array<{ kind: string; value: string }> }) => <ul>{items.map((item) => <li key={item.kind}>{item.value}</li>)}</ul>
}));
vi.mock("../src/components/ui", () => ({
  Avatar: ({ name }: { name: string }) => <span>{name}</span>,
  Badge: ({ children }: { children: React.ReactNode }) => <span>{children}</span>,
  BottomNav: () => null,
  Button: ({ children, ...props }: React.ButtonHTMLAttributes<HTMLButtonElement>) => <button {...props}>{children}</button>,
  Card: ({ children }: { children: React.ReactNode }) => <section>{children}</section>,
  ErrorState: ({ title, onRetry }: { title: string; onRetry?: () => void }) => <section><h1>{title}</h1>{onRetry && <button onClick={onRetry}>retry</button>}</section>,
  LoadingState: ({ title }: { title: string }) => <p>{title}</p>,
  Modal: ({ children, open, title }: { children: React.ReactNode; open: boolean; title: string }) => open ? <section aria-label={title}>{children}</section> : null,
  Textarea: (props: React.TextareaHTMLAttributes<HTMLTextAreaElement>) => <textarea {...props} />,
  Toast: () => null
}));

import { DealDetails } from "../src/pages/DealDetails";
import { clearDealCache, getCachedDeal } from "../src/data/dealCache";

const ru = (key: string) => translate(key, undefined, "ru");
const terms = { title: "Coffee launch", description: "Agreed description", city: "tashkent", categories: ["beauty"], requirements: ["One reel"], budgetFrom: 100, budgetTo: 200, deadline: null };
const activeDeal = {
  id: "deal-a", status: 0, sourceType: "campaignApplication", termsSource: "snapshot", terms, counterpartyName: "Lumi Beauty", counterpartyImageUrl: null,
  campaignApplicationId: "application-a", collaborationRequestId: null, createdAtUtc: "2026-09-01T00:00:00Z", completedAtUtc: null, canComplete: true, canReview: false, hasReviewed: false
};
const completedDeal = { ...activeDeal, status: 1, completedAtUtc: "2026-09-05T00:00:00Z", canComplete: false, canReview: true };
const renderDeal = () => render(<I18nProvider><DealDetails id="deal-a" /></I18nProvider>);

describe("Deal details", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    clearDealCache();
    api.getMyDeal.mockResolvedValue(activeDeal);
    api.getDealContact.mockResolvedValue({ telegram: "@lumi", phone: null, email: null, websiteUrl: null });
    api.completeDeal.mockResolvedValue({});
    api.createDealReview.mockResolvedValue({});
  });

  afterEach(() => clearDealCache());

  it("shows the snapshot terms, counterparty and source", async () => {
    renderDeal();

    expect(await screen.findByRole("heading", { name: "Coffee launch" })).toBeInTheDocument();
    expect(screen.getAllByText("Lumi Beauty").length).toBeGreaterThan(0);
    expect(screen.getByText(ru("deals.source.campaignApplication"))).toBeInTheDocument();
    expect(screen.getByText("One reel")).toBeInTheDocument();
    expect(screen.queryByText(ru("deals.termsFallbackNote"))).not.toBeInTheDocument();
  });

  it("marks live campaign fallback terms as not historical", async () => {
    api.getMyDeal.mockResolvedValue({ ...activeDeal, termsSource: "liveCampaignFallback" });
    renderDeal();

    expect(await screen.findByText(ru("deals.termsFallbackNote"))).toBeInTheDocument();
  });

  it("shows collaboration deals without campaign terms", async () => {
    api.getMyDeal.mockResolvedValue({ ...activeDeal, sourceType: "collaborationRequest", termsSource: "collaboration", terms: null, campaignApplicationId: null, collaborationRequestId: "request-a" });
    renderDeal();

    expect(await screen.findByRole("heading", { name: ru("deals.source.collaborationRequest") })).toBeInTheDocument();
    expect(screen.getByText(ru("deals.collaborationNote"))).toBeInTheDocument();
    expect(screen.queryByText(ru("deals.terms"))).not.toBeInTheDocument();
  });

  it("completes once after confirmation and refreshes the deal", async () => {
    api.getMyDeal.mockResolvedValueOnce(activeDeal).mockResolvedValue(completedDeal);
    renderDeal();
    fireEvent.click(await screen.findByRole("button", { name: ru("requests.complete") }));
    const confirm = within(screen.getByLabelText(ru("deals.completeTitle"))).getByRole("button", { name: ru("requests.complete") });
    fireEvent.click(confirm);
    fireEvent.click(confirm);

    expect(await screen.findByRole("button", { name: ru("requests.publishReview") })).toBeInTheDocument();
    expect(api.completeDeal).toHaveBeenCalledTimes(1);
    expect(api.completeDeal).toHaveBeenCalledWith("deal-a");
    expect(getCachedDeal("deal-a")?.status).toBe(1);
    expect(screen.queryByRole("button", { name: ru("requests.complete") })).not.toBeInTheDocument();
  });

  it("reconciles a completion conflict by reloading the deal", async () => {
    api.getMyDeal.mockResolvedValueOnce(activeDeal).mockResolvedValue(completedDeal);
    api.completeDeal.mockRejectedValue(new ApiError(409, "conflict"));
    renderDeal();
    fireEvent.click(await screen.findByRole("button", { name: ru("requests.complete") }));
    fireEvent.click(within(screen.getByLabelText(ru("deals.completeTitle"))).getByRole("button", { name: ru("requests.complete") }));

    await waitFor(() => expect(api.getMyDeal).toHaveBeenCalledTimes(2));
    expect(await screen.findByRole("button", { name: ru("requests.publishReview") })).toBeInTheDocument();
  });

  it("publishes a review with the selected rating and shows the reviewed state", async () => {
    api.getMyDeal.mockResolvedValueOnce(completedDeal).mockResolvedValue({ ...completedDeal, canReview: false, hasReviewed: true });
    renderDeal();
    fireEvent.click(await screen.findByRole("button", { name: `${ru("requests.rating")} 4` }));
    fireEvent.change(screen.getByPlaceholderText(ru("requests.reviewPlaceholder")), { target: { value: "Great" } });
    fireEvent.click(screen.getByRole("button", { name: ru("requests.publishReview") }));

    expect(await screen.findByText(ru("deals.reviewSent"))).toBeInTheDocument();
    expect(api.createDealReview).toHaveBeenCalledWith("deal-a", 4, "Great");
  });

  it("shows a not-found state for foreign or missing deals", async () => {
    api.getMyDeal.mockRejectedValue(new ApiError(404, "not_found"));
    renderDeal();

    expect(await screen.findByRole("heading", { name: ru("deals.notFoundTitle") })).toBeInTheDocument();
  });

  it("shows the counterparty contacts inside the deal", async () => {
    renderDeal();

    expect(await screen.findByText("@lumi")).toBeInTheDocument();
    expect(screen.getByText(ru("deals.contacts"))).toBeInTheDocument();
    expect(api.getDealContact).toHaveBeenCalledWith("deal-a");
  });

  it("hides the contacts block when the contact is unavailable", async () => {
    api.getDealContact.mockRejectedValue(new ApiError(404, "not_found"));
    renderDeal();
    await screen.findByRole("heading", { name: "Coffee launch" });

    expect(screen.queryByText(ru("deals.contacts"))).not.toBeInTheDocument();
  });
  it("explains blind reviews and the review deadline", async () => {
    api.getMyDeal.mockResolvedValue({ ...completedDeal, reviewDeadlineUtc: "2026-09-19T00:00:00Z" });
    renderDeal();

    expect(await screen.findByText(ru("deals.reviewBlindHint"))).toBeInTheDocument();
    const date = new Intl.DateTimeFormat("ru-RU", { day: "numeric", month: "short", year: "numeric" }).format(new Date("2026-09-19T00:00:00Z"));
    expect(screen.getByText(translate("deals.reviewDeadline", { date }, "ru"))).toBeInTheDocument();
  });

  it("tells that the partner already reviewed and hides only the content", async () => {
    api.getMyDeal.mockResolvedValue({ ...completedDeal, partnerHasReviewed: true });
    renderDeal();

    expect(await screen.findByText(translate("deals.partnerReviewedHint", { partner: completedDeal.counterpartyName }, "ru"))).toBeInTheDocument();
    expect(screen.queryByText(ru("deals.reviewBlindHint"))).not.toBeInTheDocument();
  });

  it("tells the participant when the review window has closed", async () => {
    api.getMyDeal.mockResolvedValue({ ...completedDeal, canReview: false, reviewDeadlineUtc: "2026-09-19T00:00:00Z" });
    renderDeal();

    expect(await screen.findByText(ru("deals.reviewClosed"))).toBeInTheDocument();
    expect(screen.queryByText(ru("requests.publishReview"))).not.toBeInTheDocument();
  });

  it("does not claim the window closed after the participant reviewed", async () => {
    api.getMyDeal.mockResolvedValue({ ...completedDeal, canReview: false, hasReviewed: true, reviewDeadlineUtc: "2026-09-19T00:00:00Z" });
    renderDeal();

    expect(await screen.findByText(ru("deals.reviewSent"))).toBeInTheDocument();
    expect(screen.queryByText(ru("deals.reviewClosed"))).not.toBeInTheDocument();
  });
});
