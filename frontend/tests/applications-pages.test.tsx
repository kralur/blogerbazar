import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { ApiError } from "../src/api/client";
import { I18nProvider, translate } from "../src/i18n";

const api = vi.hoisted(() => ({
  acceptMyCampaignApplication: vi.fn(),
  getCampaignApplicationInbox: vi.fn(),
  getCurrentPlatformUser: vi.fn(),
  getMyCampaignApplication: vi.fn(),
  getMyCampaignApplicationsPage: vi.fn(),
  rejectMyCampaignApplication: vi.fn(),
  withdrawMyCampaignApplication: vi.fn()
}));

vi.mock("../src/api/marketplace", async (importOriginal) => ({
  ...(await importOriginal<typeof import("../src/api/marketplace")>()),
  acceptMyCampaignApplication: api.acceptMyCampaignApplication,
  getCampaignApplicationInbox: api.getCampaignApplicationInbox,
  getCurrentPlatformUser: api.getCurrentPlatformUser,
  getMyCampaignApplication: api.getMyCampaignApplication,
  getMyCampaignApplicationsPage: api.getMyCampaignApplicationsPage,
  rejectMyCampaignApplication: api.rejectMyCampaignApplication,
  withdrawMyCampaignApplication: api.withdrawMyCampaignApplication
}));
vi.mock("../src/components/ManagementBackLink", () => ({ ManagementBackLink: () => null }));
vi.mock("../src/components/ui", () => ({
  Avatar: ({ name }: { name: string }) => <span>{name}</span>,
  Badge: ({ children }: { children: React.ReactNode }) => <span>{children}</span>,
  BottomNav: () => null,
  Button: ({ children, ...props }: React.ButtonHTMLAttributes<HTMLButtonElement>) => <button {...props}>{children}</button>,
  Card: ({ children }: { children: React.ReactNode }) => <section>{children}</section>,
  ErrorState: ({ title, onRetry }: { title: string; onRetry?: () => void }) => <section><h1>{title}</h1>{onRetry && <button onClick={onRetry}>retry</button>}</section>,
  LoadingState: ({ title }: { title: string }) => <p>{title}</p>,
  Modal: ({ children, open, title }: { children: React.ReactNode; open: boolean; title: string }) => open ? <section aria-label={title}>{children}</section> : null,
  Toast: () => null
}));
vi.mock("../src/components/catalog/CatalogShared", () => ({
  CatalogState: ({ title }: { title: string }) => <p>{title}</p>,
  FilterSelect: ({ label, options, onChange, value }: { label: string; options: string[][]; onChange: (value: string) => void; value: string }) => <label>{label}<select onChange={(event) => onChange(event.target.value)} value={value}>{options.map(([optionValue, text]) => <option key={optionValue} value={optionValue}>{text}</option>)}</select></label>,
  SearchSkeleton: () => <p>loading</p>
}));

import { MyApplicationDetails } from "../src/pages/MyApplicationDetails";
import { MyCampaignApplications } from "../src/pages/MyCampaignApplications";
import { BloggerApplications } from "../src/pages/BloggerApplications";
import { clearCampaignApplicationCache } from "../src/data/campaignApplicationCache";
import { RootScreenVisibility } from "../src/navigation/RootScreenVisibility";

const application = {
  id: "application-a", campaignId: "campaign-a", campaignTitle: "Coffee launch", businessName: "Lumi Beauty", city: "tashkent", categories: ["beauty"], minBudget: null, maxBudget: null, deadline: null, message: "Ready", status: 0, createdAtUtc: "2026-09-01T00:00:00Z", campaignDescription: "Description", requirements: []
};
const inboxItem = { id: "application-a", bloggerId: "blogger-a", bloggerName: "Ali", city: "tashkent", categories: ["beauty"], message: "Ready", status: 0, createdAtUtc: "2026-09-01T00:00:00Z" };
const page = (items = [inboxItem]) => ({ items, total: items.length, page: 1, pageSize: 20, hasMore: false });

describe("application mutation screens", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    clearCampaignApplicationCache();
    api.getMyCampaignApplication.mockResolvedValue(application);
    api.getMyCampaignApplicationsPage.mockResolvedValue({ items: [], total: 0, page: 1, pageSize: 20, hasMore: false });
    api.withdrawMyCampaignApplication.mockResolvedValue({ id: "application-a", status: 4 });
    api.getCurrentPlatformUser.mockResolvedValue({ selectedMarketplaceRole: "Business" });
    api.getCampaignApplicationInbox.mockResolvedValue(page());
    api.acceptMyCampaignApplication.mockResolvedValue({ id: "application-a", status: 2, dealId: "deal-a" });
    api.rejectMyCampaignApplication.mockResolvedValue({ id: "application-a", status: 3 });
  });

  afterEach(() => { clearCampaignApplicationCache(); vi.clearAllMocks(); });

  it("withdraws once and removes the pending action after success", async () => {
    render(<I18nProvider><MyApplicationDetails id="application-a" /></I18nProvider>);
    await screen.findByText("Coffee launch");
    fireEvent.click(screen.getByRole("button", { name: translate("applications.withdraw", undefined, "ru") }));
    const confirm = within(screen.getByLabelText(translate("applications.withdrawTitle", undefined, "ru"))).getByRole("button", { name: translate("applications.withdraw", undefined, "ru") });
    fireEvent.click(confirm);
    fireEvent.click(confirm);
    expect(api.withdrawMyCampaignApplication).toHaveBeenCalledTimes(1);
    await waitFor(() => expect(screen.queryByRole("button", { name: translate("applications.withdraw", undefined, "ru") })).not.toBeInTheDocument());
  });

  it("reconciles a withdraw conflict to the server final state", async () => {
    api.withdrawMyCampaignApplication.mockRejectedValueOnce(new ApiError(409));
    api.getMyCampaignApplication.mockResolvedValueOnce(application).mockResolvedValueOnce({ ...application, status: 2 });
    render(<I18nProvider><MyApplicationDetails id="application-a" /></I18nProvider>);
    await screen.findByText("Coffee launch");
    fireEvent.click(screen.getByRole("button", { name: translate("applications.withdraw", undefined, "ru") }));
    fireEvent.click(within(screen.getByLabelText(translate("applications.withdrawTitle", undefined, "ru"))).getByRole("button", { name: translate("applications.withdraw", undefined, "ru") }));
    await screen.findByText(translate("applications.status.accepted", undefined, "ru"));
    expect(screen.queryByRole("button", { name: translate("applications.withdraw", undefined, "ru") })).not.toBeInTheDocument();
  });

  it.each([[403, "applications.deniedTitle"], [404, "applications.notFoundTitle"]] as const)("maps withdraw %s to a safe screen state", async (status, key) => {
    api.withdrawMyCampaignApplication.mockRejectedValueOnce(new ApiError(status));
    render(<I18nProvider><MyApplicationDetails id="application-a" /></I18nProvider>);
    await screen.findByText("Coffee launch");
    fireEvent.click(screen.getByRole("button", { name: translate("applications.withdraw", undefined, "ru") }));
    fireEvent.click(within(screen.getByLabelText(translate("applications.withdrawTitle", undefined, "ru"))).getByRole("button", { name: translate("applications.withdraw", undefined, "ru") }));
    await screen.findByText(translate(key, undefined, "ru"));
  });

  it.each([new ApiError(422), new Error("network unavailable")])("keeps the application available after a recoverable withdraw failure", async (error) => {
    api.withdrawMyCampaignApplication.mockRejectedValueOnce(error);
    render(<I18nProvider><MyApplicationDetails id="application-a" /></I18nProvider>);
    await screen.findByText("Coffee launch");
    fireEvent.click(screen.getByRole("button", { name: translate("applications.withdraw", undefined, "ru") }));
    fireEvent.click(within(screen.getByLabelText(translate("applications.withdrawTitle", undefined, "ru"))).getByRole("button", { name: translate("applications.withdraw", undefined, "ru") }));
    await waitFor(() => expect(api.withdrawMyCampaignApplication).toHaveBeenCalledTimes(1));
    expect(screen.getByText("Coffee launch")).toBeInTheDocument();
    expect(screen.getAllByRole("button", { name: translate("applications.withdraw", undefined, "ru") })).not.toHaveLength(0);
  });

  it.each(["accept", "reject"] as const)("submits an inbox %s once and removes final-state actions", async (action) => {
    const mutation = action === "accept" ? api.acceptMyCampaignApplication : api.rejectMyCampaignApplication;
    const title = action === "accept" ? "applications.acceptTitle" : "applications.rejectTitle";
    mutation.mockResolvedValueOnce({ id: "application-a", status: action === "accept" ? 2 : 3 });
    render(<I18nProvider><MyCampaignApplications campaignId="campaign-a" /></I18nProvider>);
    await screen.findByRole("heading", { name: "Ali" });
    fireEvent.click(screen.getByRole("button", { name: translate(`applications.${action}`, undefined, "ru") }));
    const confirm = within(screen.getByLabelText(translate(title, undefined, "ru"))).getByRole("button", { name: translate(`applications.${action}`, undefined, "ru") });
    fireEvent.click(confirm);
    fireEvent.click(confirm);
    expect(mutation).toHaveBeenCalledTimes(1);
    await screen.findByText(translate(action === "accept" ? "applications.status.accepted" : "applications.status.rejected", undefined, "ru"));
    expect(screen.queryByRole("button", { name: translate("applications.accept", undefined, "ru") })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: translate("applications.reject", undefined, "ru") })).not.toBeInTheDocument();
  });

  it.each(["accept", "reject"] as const)("reconciles a %s conflict before restoring interaction", async (action) => {
    const mutation = action === "accept" ? api.acceptMyCampaignApplication : api.rejectMyCampaignApplication;
    mutation.mockRejectedValueOnce(new ApiError(409));
    api.getCampaignApplicationInbox.mockResolvedValueOnce(page()).mockResolvedValueOnce(page([{ ...inboxItem, status: action === "accept" ? 2 : 3 }]));
    render(<I18nProvider><MyCampaignApplications campaignId="campaign-a" /></I18nProvider>);
    await screen.findByRole("heading", { name: "Ali" });
    fireEvent.click(screen.getByRole("button", { name: translate(`applications.${action}`, undefined, "ru") }));
    fireEvent.click(within(screen.getByLabelText(translate(action === "accept" ? "applications.acceptTitle" : "applications.rejectTitle", undefined, "ru"))).getByRole("button", { name: translate(`applications.${action}`, undefined, "ru") }));
    await screen.findByText(translate(action === "accept" ? "applications.status.accepted" : "applications.status.rejected", undefined, "ru"));
    expect(screen.queryByRole("button", { name: translate("applications.accept", undefined, "ru") })).not.toBeInTheDocument();
  });

  it.each([
    ["accept", 403, "applications.deniedTitle"],
    ["accept", 404, "applications.notFoundTitle"],
    ["reject", 403, "applications.deniedTitle"],
    ["reject", 404, "applications.notFoundTitle"]
  ] as const)("maps %s inbox mutation %s to a safe screen state", async (action, status, key) => {
    const mutation = action === "accept" ? api.acceptMyCampaignApplication : api.rejectMyCampaignApplication;
    mutation.mockRejectedValueOnce(new ApiError(status));
    render(<I18nProvider><MyCampaignApplications campaignId="campaign-a" /></I18nProvider>);
    await screen.findByRole("heading", { name: "Ali" });
    fireEvent.click(screen.getByRole("button", { name: translate(`applications.${action}`, undefined, "ru") }));
    fireEvent.click(within(screen.getByLabelText(translate(action === "accept" ? "applications.acceptTitle" : "applications.rejectTitle", undefined, "ru"))).getByRole("button", { name: translate(`applications.${action}`, undefined, "ru") }));
    await screen.findByText(translate(key, undefined, "ru"));
  });

  it.each(["accept", "reject"] as const)("keeps inbox data available after a recoverable %s failure", async (action) => {
    const mutation = action === "accept" ? api.acceptMyCampaignApplication : api.rejectMyCampaignApplication;
    mutation.mockRejectedValueOnce(new ApiError(422));
    render(<I18nProvider><MyCampaignApplications campaignId="campaign-a" /></I18nProvider>);
    await screen.findByRole("heading", { name: "Ali" });
    fireEvent.click(screen.getByRole("button", { name: translate(`applications.${action}`, undefined, "ru") }));
    fireEvent.click(within(screen.getByLabelText(translate(action === "accept" ? "applications.acceptTitle" : "applications.rejectTitle", undefined, "ru"))).getByRole("button", { name: translate(`applications.${action}`, undefined, "ru") }));
    await waitFor(() => expect(mutation).toHaveBeenCalledTimes(1));
    expect(screen.getByRole("heading", { name: "Ali" })).toBeInTheDocument();
  });

  it("does not fetch Blogger applications while its cached root screen is hidden", async () => {
    const view = render(<I18nProvider><RootScreenVisibility active={false}><BloggerApplications activeMarketplaceRole="Blogger" /></RootScreenVisibility></I18nProvider>);
    await Promise.resolve();
    expect(api.getMyCampaignApplicationsPage).not.toHaveBeenCalled();

    view.rerender(<I18nProvider><RootScreenVisibility active><BloggerApplications activeMarketplaceRole="Blogger" /></RootScreenVisibility></I18nProvider>);
    await waitFor(() => expect(api.getMyCampaignApplicationsPage).toHaveBeenCalledTimes(1));
  });

  it("opens the deal created by an inbox accept", async () => {
    render(<I18nProvider><MyCampaignApplications campaignId="campaign-a" /></I18nProvider>);
    await screen.findByRole("heading", { name: "Ali" });
    fireEvent.click(screen.getByRole("button", { name: translate("applications.accept", undefined, "ru") }));
    fireEvent.click(within(screen.getByLabelText(translate("applications.acceptTitle", undefined, "ru"))).getByRole("button", { name: translate("applications.accept", undefined, "ru") }));

    fireEvent.click(await screen.findByRole("button", { name: translate("deals.open", undefined, "ru") }));

    expect(window.location.hash).toBe("#/deal/deal-a");
  });

  it("lets a blogger open the deal of an accepted application", async () => {
    api.getMyCampaignApplication.mockResolvedValue({ ...application, status: 2, dealId: "deal-b" });
    render(<I18nProvider><MyApplicationDetails id="application-a" /></I18nProvider>);

    fireEvent.click(await screen.findByRole("button", { name: translate("deals.open", undefined, "ru") }));

    expect(window.location.hash).toBe("#/deal/deal-b");
    expect(screen.queryByRole("button", { name: translate("applications.withdraw", undefined, "ru") })).not.toBeInTheDocument();
  });

  it("does not offer a deal for an application without one", async () => {
    render(<I18nProvider><MyApplicationDetails id="application-a" /></I18nProvider>);
    await screen.findByText("Coffee launch");

    expect(screen.queryByRole("button", { name: translate("deals.open", undefined, "ru") })).not.toBeInTheDocument();
  });
});
