import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { ApiError } from "../src/api/client";
import { I18nProvider, translate } from "../src/i18n";

const api = vi.hoisted(() => ({ getMyOffer: vi.fn(), acceptOffer: vi.fn(), declineOffer: vi.fn(), createOffer: vi.fn(), getMyCampaigns: vi.fn(), getMyCampaign: vi.fn() }));

vi.mock("../src/api/marketplace", async (importOriginal) => ({
  ...(await importOriginal<typeof import("../src/api/marketplace")>()),
  getMyOffer: api.getMyOffer,
  acceptOffer: api.acceptOffer,
  declineOffer: api.declineOffer,
  createOffer: api.createOffer,
  getMyCampaigns: api.getMyCampaigns,
  getMyCampaign: api.getMyCampaign
}));
vi.mock("../src/components/ManagementBackLink", () => ({ ManagementBackLink: () => null }));
vi.mock("../src/telegram/TelegramProvider", async (importOriginal) => ({ ...(await importOriginal<typeof import("../src/telegram/TelegramProvider")>()), useTelegram: () => ({ haptic: { selection: vi.fn(), success: vi.fn(), error: vi.fn(), warning: vi.fn() }, openLink: vi.fn(), registerBackButtonHandler: vi.fn(() => vi.fn()) }) }));
vi.mock("../src/components/ui", () => ({
  Avatar: () => null,
  Badge: ({ children }: { children: React.ReactNode }) => <span>{children}</span>,
  BottomNav: () => null,
  Button: ({ children, ...props }: React.ButtonHTMLAttributes<HTMLButtonElement>) => <button {...props}>{children}</button>,
  Card: ({ children }: { children: React.ReactNode }) => <section>{children}</section>,
  ErrorState: ({ title }: { title: string }) => <h1>{title}</h1>,
  Input: ({ label, ...props }: React.InputHTMLAttributes<HTMLInputElement> & { label: string }) => <label>{label}<input {...props} /></label>,
  LoadingState: ({ title }: { title: string }) => <p>{title}</p>,
  Modal: ({ children, open, title }: { children: React.ReactNode; open: boolean; title: string }) => open ? <section aria-label={title}>{children}</section> : null,
  Textarea: ({ label, ...props }: React.TextareaHTMLAttributes<HTMLTextAreaElement> & { label: string }) => <label>{label}<textarea {...props} /></label>,
  Toast: ({ message }: { message: string }) => message ? <p role="status">{message}</p> : null
}));

import { OfferDetails } from "../src/pages/OfferDetails";
import { OfferForm } from "../src/components/OfferForm";
import { localDay } from "../src/lib/currency";

const ru = (key: string) => translate(key, undefined, "ru");
const pending = {
  id: "offer-a", bloggerId: "blogger-a", counterpartyName: "Lumi", counterpartyImageUrl: null, format: "reels", offeredBudget: 1_500_000,
  deadline: null, message: "One reel", state: "pending", createdAtUtc: "2026-10-05T00:00:00Z", expiresAtUtc: "2026-10-07T00:00:00Z", dealId: null, canRespond: true
};
const renderOffer = () => render(<I18nProvider><OfferDetails id="offer-a" /></I18nProvider>);

describe("Offer details", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    window.location.hash = "#/offer/offer-a";
    api.getMyOffer.mockResolvedValue(pending);
  });

  afterEach(() => { window.location.hash = ""; });

  it("accepts once and opens the created deal", async () => {
    api.acceptOffer.mockResolvedValue({ id: "offer-a", state: "accepted", dealId: "deal-a" });
    renderOffer();
    const accept = await screen.findByRole("button", { name: ru("offers.accept") });
    fireEvent.click(accept);
    fireEvent.click(accept);

    await waitFor(() => expect(window.location.hash).toBe("#/deal/deal-a"));
    expect(api.acceptOffer).toHaveBeenCalledTimes(1);
  });

  it("declines and refreshes the offer state", async () => {
    api.declineOffer.mockResolvedValue({ id: "offer-a", state: "declined", dealId: null });
    api.getMyOffer.mockResolvedValueOnce(pending).mockResolvedValue({ ...pending, state: "declined", canRespond: false });
    renderOffer();
    fireEvent.click(await screen.findByRole("button", { name: ru("offers.decline") }));

    expect(await screen.findByText(ru("offers.state.declined"))).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: ru("offers.accept") })).not.toBeInTheDocument();
  });

  it("reloads when the offer is no longer pending", async () => {
    api.acceptOffer.mockRejectedValue(new ApiError(409, "conflict"));
    api.getMyOffer.mockResolvedValueOnce(pending).mockResolvedValue({ ...pending, state: "expired", canRespond: false });
    renderOffer();
    fireEvent.click(await screen.findByRole("button", { name: ru("offers.accept") }));

    expect(await screen.findByText(ru("offers.state.expired"))).toBeInTheDocument();
    expect(screen.getByRole("status")).toHaveTextContent(ru("offers.noLongerPending"));
  });

  it("shows no response actions to the business that sent the offer", async () => {
    api.getMyOffer.mockResolvedValue({ ...pending, counterpartyName: "Madina", canRespond: false });
    renderOffer();
    await screen.findByText("One reel");

    expect(screen.queryByRole("button", { name: ru("offers.accept") })).not.toBeInTheDocument();
  });

  it("shows not found for a foreign offer", async () => {
    api.getMyOffer.mockRejectedValue(new ApiError(404, "not_found"));
    renderOffer();

    expect(await screen.findByRole("heading", { name: ru("offers.notFoundTitle") })).toBeInTheDocument();
  });
});

describe("Offer form", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    api.getMyCampaigns.mockResolvedValue({ items: [], total: 0, page: 1, pageSize: 20, hasMore: false });
  });

  const renderForm = (onSent = vi.fn()) => {
    render(<I18nProvider><OfferForm bloggerId="blogger-a" onClose={vi.fn()} onSent={onSent} open /></I18nProvider>);
    return onSent;
  };

  it("sends the selected format, optional budget and message", async () => {
    api.createOffer.mockResolvedValue(pending);
    const onSent = renderForm();
    fireEvent.click(screen.getByRole("button", { name: ru("card.post") }));
    fireEvent.change(screen.getByLabelText(ru("offers.budget")), { target: { value: "1 500 000" } });
    fireEvent.change(screen.getByLabelText(ru("offers.message")), { target: { value: "  Post about us  " } });
    fireEvent.click(screen.getByRole("button", { name: ru("offers.send") }));

    await waitFor(() => expect(onSent).toHaveBeenCalledWith(pending));
    expect(api.createOffer).toHaveBeenCalledWith({ bloggerId: "blogger-a", format: "post", offeredBudget: 1_500_000, deadline: new Date(`${localDay(7)}T00:00:00Z`).toISOString(), message: "Post about us" });
  });

  it("fills budget, deadline and message from an open campaign but keeps them editable", async () => {
    const future = "2030-05-20T00:00:00Z";
    api.getMyCampaigns.mockResolvedValue({ items: [
      { id: "open", title: "Coffee launch", categories: [], minBudget: 500_000, maxBudget: 900_000, deadline: future, status: 1, isPromoted: false, createdAtUtc: "", updatedAtUtc: "", applicationsCount: 0 },
      { id: "old", title: "Old", categories: [], deadline: "2020-01-01T00:00:00Z", status: 1, isPromoted: false, createdAtUtc: "", updatedAtUtc: "", applicationsCount: 0 }
    ], total: 2, page: 1, pageSize: 20, hasMore: false });
    api.getMyCampaign.mockResolvedValue({ id: "open", title: "Coffee launch", description: "Reels about our new coffee", requirements: [], categories: [], minBudget: 500_000, maxBudget: 900_000, deadline: future, status: 1, isPromoted: false, createdAtUtc: "", updatedAtUtc: "", applicationsCount: 0 });
    renderForm();

    const select = await screen.findByLabelText(ru("offers.fromCampaign"));
    expect(screen.queryByRole("option", { name: "Old" })).not.toBeInTheDocument();
    fireEvent.change(select, { target: { value: "open" } });

    await waitFor(() => expect(screen.getByLabelText(ru("offers.message"))).toHaveValue("Coffee launch\n\nReels about our new coffee"));
    expect(screen.getByLabelText(ru("offers.budget"))).toHaveValue("900000");
    expect(screen.getByLabelText(ru("offers.deadline"))).toHaveValue("2030-05-20");
  });

  it("does not offer campaign prefill when the business has no open campaigns", async () => {
    renderForm();
    await waitFor(() => expect(api.getMyCampaigns).toHaveBeenCalled());
    expect(screen.queryByLabelText(ru("offers.fromCampaign"))).not.toBeInTheDocument();
  });

  it("requires a message before sending", async () => {
    renderForm();
    fireEvent.click(screen.getByRole("button", { name: ru("offers.send") }));

    expect(await screen.findByRole("alert")).toHaveTextContent(ru("offers.messageRequired"));
    expect(api.createOffer).not.toHaveBeenCalled();
  });

  it("explains a conflict", async () => {
    api.createOffer.mockRejectedValue(new ApiError(409, "conflict"));
    renderForm();
    fireEvent.change(screen.getByLabelText(ru("offers.message")), { target: { value: "Hi" } });
    fireEvent.click(screen.getByRole("button", { name: ru("offers.send") }));

    expect(await screen.findByRole("alert")).toHaveTextContent(ru("offers.conflict"));
  });
});
