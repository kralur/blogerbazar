import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ApiError } from "../src/api/client";
import { I18nProvider, translate } from "../src/i18n";

const api = vi.hoisted(() => ({
  getMyDeal: vi.fn(), getDealContact: vi.fn(), completeDeal: vi.fn(), createDealReview: vi.fn(),
  setDealPrice: vi.fn(), addDealPublication: vi.fn(), updateDealPublicationViews: vi.fn(), deleteDealPublication: vi.fn(), confirmDealPublication: vi.fn()
}));

vi.mock("../src/api/marketplace", async (importOriginal) => ({ ...(await importOriginal<typeof import("../src/api/marketplace")>()), ...api }));
vi.mock("../src/telegram/TelegramProvider", () => ({ useTelegram: () => ({ haptic: { impact: vi.fn(), success: vi.fn(), selection: vi.fn(), error: vi.fn(), warning: vi.fn() } }) }));
vi.mock("../src/components/ContactList", () => ({ hasContacts: () => false, ContactList: () => null }));
vi.mock("../src/components/ui", () => ({
  Avatar: ({ name }: { name: string }) => <span>{name}</span>,
  Badge: ({ children }: { children: React.ReactNode }) => <span>{children}</span>,
  BottomNav: () => null,
  Button: ({ children, ...props }: React.ButtonHTMLAttributes<HTMLButtonElement>) => <button {...props}>{children}</button>,
  Card: ({ children }: { children: React.ReactNode }) => <section>{children}</section>,
  ErrorState: ({ title }: { title: string }) => <h1>{title}</h1>,
  Input: ({ label, suffix: _suffix, ...props }: React.InputHTMLAttributes<HTMLInputElement> & { label?: string; suffix?: React.ReactNode }) => <label>{label}<input {...props} /></label>,
  LoadingState: ({ title }: { title: string }) => <p>{title}</p>,
  Modal: ({ children, open, title }: { children: React.ReactNode; open: boolean; title: string }) => open ? <section aria-label={title}>{children}</section> : null,
  Textarea: (props: React.TextareaHTMLAttributes<HTMLTextAreaElement>) => <textarea {...props} />,
  Toast: ({ message }: { message: string }) => message ? <p role="status">{message}</p> : null
}));

import { DealDetails } from "../src/pages/DealDetails";
import { clearDealCache } from "../src/data/dealCache";

const ru = (key: string, params?: Record<string, string | number>) => translate(key, params, "ru");
const base = {
  id: "deal-a", status: 0, sourceType: "campaignApplication", termsSource: "snapshot", terms: null, counterpartyName: "Lumi Beauty", counterpartyImageUrl: null,
  campaignApplicationId: "application-a", collaborationRequestId: null, createdAtUtc: "2026-09-01T00:00:00Z", completedAtUtc: null, canComplete: true, canReview: false, hasReviewed: false
};
const creatorDeal = { ...base, agreedPrice: 1_500_000, canSetPrice: false, publications: [], canAddPublication: true, canConfirmPublications: false };
const businessDeal = { ...base, agreedPrice: null, canSetPrice: true, publications: [], canAddPublication: false, canConfirmPublications: true };
const publication = { id: "pub-1", url: "https://instagram.com/p/abc", views: 4000, confirmed: false, createdAtUtc: "2026-09-02T00:00:00Z" };
const renderDeal = () => render(<I18nProvider><DealDetails id="deal-a" /></I18nProvider>);

describe("Deal results (D51)", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    clearDealCache();
    api.getDealContact.mockResolvedValue({});
    api.completeDeal.mockResolvedValue({});
    api.setDealPrice.mockResolvedValue({ agreedPrice: 2_000_000 });
    api.addDealPublication.mockResolvedValue(publication);
    api.confirmDealPublication.mockResolvedValue({ ...publication, confirmed: true });
  });

  it("stays hidden for an older API without results", async () => {
    api.getMyDeal.mockResolvedValue(base);
    renderDeal();

    expect((await screen.findAllByText("Lumi Beauty")).length).toBeGreaterThan(0);
    expect(screen.queryByText(ru("deals.results"))).not.toBeInTheDocument();
  });

  it("lets the creator add a link with views", async () => {
    api.getMyDeal.mockResolvedValue(creatorDeal);
    renderDeal();

    fireEvent.click(await screen.findByRole("button", { name: ru("deals.addPublication") }));
    const dialog = screen.getByLabelText(ru("deals.addPublication"));
    fireEvent.change(within(dialog).getByLabelText(ru("deals.publicationLink")), { target: { value: "http://bad" } });
    fireEvent.click(within(dialog).getByRole("button", { name: ru("deals.saveResult") }));
    expect(within(dialog).getByRole("alert")).toHaveTextContent(ru("deals.publicationLinkInvalid"));
    expect(api.addDealPublication).not.toHaveBeenCalled();

    fireEvent.change(within(dialog).getByLabelText(ru("deals.publicationLink")), { target: { value: "https://instagram.com/p/abc" } });
    fireEvent.change(within(dialog).getByLabelText(ru("deals.viewsOptional")), { target: { value: "4 000" } });
    fireEvent.click(within(dialog).getByRole("button", { name: ru("deals.saveResult") }));

    await waitFor(() => expect(api.addDealPublication).toHaveBeenCalledWith("deal-a", "https://instagram.com/p/abc", 4000));
    expect(screen.getByText(ru("deals.agreedPrice"))).toBeInTheDocument();
  });

  it("lets the business confirm a publication and set the price", async () => {
    api.getMyDeal.mockResolvedValue({ ...businessDeal, publications: [publication] });
    renderDeal();

    expect(await screen.findByText(ru("deals.viewsValue", { views: "4 000" }))).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: ru("deals.addPublication") })).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: ru("deals.confirmPublication") }));
    await waitFor(() => expect(api.confirmDealPublication).toHaveBeenCalledWith("deal-a", "pub-1"));

    fireEvent.click(screen.getByRole("button", { name: ru("deals.setPrice") }));
    const dialog = screen.getByLabelText(ru("deals.setPrice"));
    fireEvent.change(within(dialog).getByLabelText(ru("deals.agreedPrice")), { target: { value: "2000000" } });
    fireEvent.click(within(dialog).getByRole("button", { name: ru("deals.saveResult") }));
    await waitFor(() => expect(api.setDealPrice).toHaveBeenCalledWith("deal-a", 2_000_000));
  });

  it("asks the creator for an optional link when completing", async () => {
    api.getMyDeal.mockResolvedValue(creatorDeal);
    renderDeal();

    fireEvent.click(await screen.findByRole("button", { name: ru("requests.complete") }));
    const dialog = screen.getByLabelText(ru("deals.completeTitle"));
    fireEvent.change(within(dialog).getByLabelText(ru("deals.completeLinkLabel")), { target: { value: "https://t.me/channel/5" } });
    fireEvent.click(within(dialog).getByRole("button", { name: ru("requests.complete") }));

    await waitFor(() => expect(api.completeDeal).toHaveBeenCalledWith("deal-a"));
    expect(api.addDealPublication).toHaveBeenCalledWith("deal-a", "https://t.me/channel/5");
    expect(api.addDealPublication.mock.invocationCallOrder[0]).toBeLessThan(api.completeDeal.mock.invocationCallOrder[0]);
  });

  it("completes without results when the fields stay empty", async () => {
    api.getMyDeal.mockResolvedValue(businessDeal);
    renderDeal();

    fireEvent.click(await screen.findByRole("button", { name: ru("requests.complete") }));
    const dialog = screen.getByLabelText(ru("deals.completeTitle"));
    expect(within(dialog).getByLabelText(ru("deals.completePriceLabel"))).toBeInTheDocument();
    fireEvent.click(within(dialog).getByRole("button", { name: ru("requests.complete") }));

    await waitFor(() => expect(api.completeDeal).toHaveBeenCalledWith("deal-a"));
    expect(api.setDealPrice).not.toHaveBeenCalled();
    expect(api.addDealPublication).not.toHaveBeenCalled();
  });

  it("completes on a retry even when the link was saved by the first attempt", async () => {
    api.getMyDeal.mockResolvedValue(creatorDeal);
    api.addDealPublication.mockRejectedValue(new ApiError(409, "publication_duplicate"));
    renderDeal();

    fireEvent.click(await screen.findByRole("button", { name: ru("requests.complete") }));
    const dialog = screen.getByLabelText(ru("deals.completeTitle"));
    fireEvent.change(within(dialog).getByLabelText(ru("deals.completeLinkLabel")), { target: { value: "https://t.me/channel/5" } });
    fireEvent.click(within(dialog).getByRole("button", { name: ru("requests.complete") }));

    await waitFor(() => expect(api.completeDeal).toHaveBeenCalledWith("deal-a"));
  });

  it("sends a publication confirmation once on a double tap", async () => {
    api.getMyDeal.mockResolvedValue({ ...businessDeal, publications: [publication] });
    let release: (value: unknown) => void = () => undefined;
    api.confirmDealPublication.mockReturnValue(new Promise((resolve) => { release = resolve; }));
    renderDeal();

    const confirm = await screen.findByRole("button", { name: ru("deals.confirmPublication") });
    fireEvent.click(confirm);
    fireEvent.click(confirm);
    release({ ...publication, confirmed: true });

    await waitFor(() => expect(api.getMyDeal).toHaveBeenCalledTimes(2));
    expect(api.confirmDealPublication).toHaveBeenCalledTimes(1);
  });
});
