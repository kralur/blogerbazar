import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider, translate } from "../src/i18n";
import { ApiError } from "../src/api/client";

const api = vi.hoisted(() => ({ getMyCampaign: vi.fn(), updateMyCampaign: vi.fn() }));

vi.mock("../src/api/marketplace", async (importOriginal) => ({
  ...(await importOriginal<typeof import("../src/api/marketplace")>()),
  getMyCampaign: api.getMyCampaign,
  updateMyCampaign: api.updateMyCampaign
}));
vi.mock("../src/components/LanguageSwitcher", () => ({ LanguageSwitcher: () => null }));
vi.mock("../src/components/ManagementBackLink", () => ({ ManagementBackLink: ({ href }: { href: string }) => <a href={href}>back</a> }));
vi.mock("../src/components/CategoryMultiSelect", () => ({ CategoryMultiSelect: ({ value }: { value: string[] }) => <div>{value.join(", ")}</div> }));
vi.mock("../src/components/RegionSelect", () => ({ RegionSelect: ({ value, onChange }: { value: string; onChange: React.ChangeEventHandler<HTMLSelectElement> }) => <select aria-label="Город" onChange={onChange} value={value}><option value="">—</option><option value="tashkent">Ташкент</option></select> }));
vi.mock("../src/hooks/useUnsavedChanges", () => ({ useUnsavedChanges: () => ({ hasPendingLeave: false, cancelLeave: vi.fn(), confirmLeave: vi.fn(), requestLeave: vi.fn(), pendingHash: null }), UnsavedChangesDialog: () => null }));
vi.mock("../src/components/ui", () => ({
  BottomNav: () => <nav />,
  Button: ({ children, ...props }: React.ButtonHTMLAttributes<HTMLButtonElement>) => <button {...props}>{children}</button>,
  ErrorState: ({ title }: { title: string }) => <h1>{title}</h1>,
  Input: ({ label, suffix, error, ...props }: React.InputHTMLAttributes<HTMLInputElement> & { label?: string; suffix?: string; error?: string }) => <label>{label}<input {...props} />{suffix && <span>{suffix}</span>}{error && <span>{error}</span>}</label>,
  LoadingState: () => <p>loading</p>,
  Textarea: ({ label, ...props }: React.TextareaHTMLAttributes<HTMLTextAreaElement> & { label?: string }) => <label>{label}<textarea {...props} /></label>,
  Toast: () => null
}));

import { MyCampaignEdit } from "../src/pages/MyCampaignEdit";

const campaign = {
  id: "campaign-a", title: "Coffee launch", description: "Launch coffee", city: "tashkent", categories: ["food"], requirements: ["Reels"], minBudget: 0, maxBudget: 500_000, deadline: "2026-08-31T00:00:00Z", status: 1 as const, isPromoted: false, createdAtUtc: "2026-08-20T00:00:00Z", updatedAtUtc: "2026-08-21T00:00:00Z", applicationsCount: 0
};

describe("My Campaign edit", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    window.location.hash = "/my-campaign-edit/campaign-a";
    api.getMyCampaign.mockResolvedValue(campaign);
    api.updateMyCampaign.mockResolvedValue({ id: "campaign-a" });
  });

  it("hydrates the private owner form and preserves zero budget with a visual currency suffix", async () => {
    render(<I18nProvider><MyCampaignEdit id="campaign-a" /></I18nProvider>);
    await screen.findByDisplayValue("Coffee launch");
    expect(screen.getByDisplayValue("0")).toBeInTheDocument();
    expect(screen.getAllByText(translate("currency.uzs", undefined, "ru"))).toHaveLength(2);
    expect(screen.getByText("food")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: translate("myCampaignEdit.save", undefined, "ru") })).toBeDisabled();
  });

  it("uses the private update endpoint and keeps numeric payload values free of suffixes", async () => {
    render(<I18nProvider><MyCampaignEdit id="campaign-a" /></I18nProvider>);
    await screen.findByDisplayValue("Coffee launch");
    fireEvent.change(screen.getByDisplayValue("Coffee launch"), { target: { value: "New coffee" } });
    expect(screen.getByRole("button", { name: translate("myCampaignEdit.save", undefined, "ru") })).toBeEnabled();
    fireEvent.click(screen.getByRole("button", { name: translate("myCampaignEdit.save", undefined, "ru") }));

    await waitFor(() => expect(api.updateMyCampaign).toHaveBeenCalledWith("campaign-a", expect.objectContaining({ title: "New coffee", budgetFrom: 0, budgetTo: 500_000, deadline: "2026-08-31T00:00:00.000Z" })));
    expect(window.location.hash).toBe("#/my-campaign/campaign-a");
  });

  it("does not offer an edit form for a closed campaign", async () => {
    api.getMyCampaign.mockResolvedValueOnce({ ...campaign, status: 2 });
    render(<I18nProvider><MyCampaignEdit id="campaign-a" /></I18nProvider>);
    await screen.findByText(translate("myCampaignEdit.notFoundTitle", undefined, "ru"));
    expect(api.updateMyCampaign).not.toHaveBeenCalled();
  });

  it("keeps entered data and maps a server validation failure to its field", async () => {
    api.updateMyCampaign.mockRejectedValueOnce(new ApiError(422, "validation_failed", ["Title"]));
    render(<I18nProvider><MyCampaignEdit id="campaign-a" /></I18nProvider>);
    await screen.findByDisplayValue("Coffee launch");
    fireEvent.change(screen.getByDisplayValue("Coffee launch"), { target: { value: "New coffee" } });
    fireEvent.click(screen.getByRole("button", { name: translate("myCampaignEdit.save", undefined, "ru") }));

    await screen.findByText(translate("myCampaignEdit.validationTitle", undefined, "ru"));
    expect(screen.getByDisplayValue("New coffee")).toBeInTheDocument();
  });
});
