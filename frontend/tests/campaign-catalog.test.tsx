import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider, translate } from "../src/i18n";

const api = vi.hoisted(() => ({
  createCampaign: vi.fn(),
  getCampaignCatalog: vi.fn(),
  getCategories: vi.fn(),
  getCurrentPlatformUser: vi.fn(),
  getMyBusinessProfile: vi.fn(),
  normalizeMarketplaceRole: vi.fn((role: unknown) => role)
}));

vi.mock("../src/api/marketplace", async (importOriginal) => ({
  ...(await importOriginal<typeof import("../src/api/marketplace")>()),
  createCampaign: api.createCampaign,
  getCampaignCatalog: api.getCampaignCatalog,
  getCategories: api.getCategories,
  getCurrentPlatformUser: api.getCurrentPlatformUser,
  getMyBusinessProfile: api.getMyBusinessProfile,
  normalizeMarketplaceRole: api.normalizeMarketplaceRole
}));

vi.mock("../src/components/LanguageSwitcher", () => ({ LanguageSwitcher: () => <span>language</span> }));
vi.mock("../src/components/CampaignCard", () => ({ CampaignCard: ({ campaign }: { campaign: { id: string; title: string } }) => <a href={`#/campaign/${campaign.id}`}>{campaign.title}</a> }));
vi.mock("../src/components/CategoryMultiSelect", () => ({ CategoryMultiSelect: () => <div>categories</div> }));
vi.mock("../src/components/RegionSelect", () => ({ RegionSelect: () => <div>region</div> }));
vi.mock("../src/hooks/useProfileDataRefresh", () => ({ useProfileDataRefresh: () => undefined }));
vi.mock("../src/hooks/useScrollRestoration", () => ({ useScrollRestoration: () => undefined }));
vi.mock("../src/telegram/TelegramProvider", () => ({ useTelegram: () => ({ haptic: { selection: vi.fn(), success: vi.fn(), error: vi.fn() } }) }));
vi.mock("../src/components/ui", () => ({
  BottomNav: () => <nav aria-label="bottom-nav" />,
  BottomSheet: ({ children, open, title, onClose }: { children: React.ReactNode; open: boolean; title: string; onClose: () => void }) => open ? <section aria-label={title}><button aria-label="close-sheet" onClick={onClose} type="button">close</button>{children}</section> : null,
  Button: ({ children, ...props }: React.ButtonHTMLAttributes<HTMLButtonElement>) => <button {...props}>{children}</button>,
  FloatingActionButton: ({ ariaLabel, children, onClick }: { ariaLabel: string; children: React.ReactNode; onClick: () => void }) => <button aria-label={ariaLabel} onClick={onClick} type="button">{children}</button>,
  Icon: () => <svg />,
  Input: ({ label, ...props }: React.InputHTMLAttributes<HTMLInputElement> & { label?: string }) => <label>{label}<input {...props} /></label>,
  Modal: ({ children, open }: { children: React.ReactNode; open: boolean }) => open ? <section>{children}</section> : null,
  SearchBar: ({ value, onChange, onClear, placeholder, clearAriaLabel }: { value?: string; onChange?: React.ChangeEventHandler<HTMLInputElement>; onClear?: () => void; placeholder?: string; clearAriaLabel?: string }) => <><input aria-label={placeholder} onChange={onChange} value={value} />{value && onClear && <button aria-label={clearAriaLabel} onClick={onClear} type="button">clear</button>}</>,
  Skeleton: () => <div>loading</div>,
  Textarea: ({ label, ...props }: React.TextareaHTMLAttributes<HTMLTextAreaElement> & { label?: string }) => <label>{label}<textarea {...props} /></label>,
  Toast: () => null
}));

import { Campaigns } from "../src/pages/Campaigns";

const campaign = {
  id: "campaign-a",
  title: "Coffee launch",
  businessName: "Coffee Co",
  businessAvatarUrl: null,
  city: "tashkent-city",
  categories: ["food"],
  requirements: ["Reels"],
  minBudget: 100_000,
  maxBudget: 300_000,
  deadline: "2026-08-31T00:00:00Z",
  status: 1,
  isPromoted: true,
  createdAtUtc: "2026-08-20T00:00:00Z"
};

function catalogResponse(overrides: Partial<{ items: typeof campaign[]; total: number; page: number; pageSize: number; hasMore: boolean }> = {}) {
  return { items: [campaign], total: 1, page: 1, pageSize: 20, hasMore: false, ...overrides };
}

function renderCampaigns() {
  return render(<I18nProvider><Campaigns /></I18nProvider>);
}

describe("Campaign catalog", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    api.getCampaignCatalog.mockResolvedValue(catalogResponse());
    api.getCategories.mockResolvedValue(["beauty", "food"]);
    api.getCurrentPlatformUser.mockResolvedValue({ selectedMarketplaceRole: "Business" });
    api.getMyBusinessProfile.mockResolvedValue({ id: "business-a" });
  });

  afterEach(() => cleanup());

  it("loads the new paged catalog with promoted as the default server-side sort", async () => {
    renderCampaigns();

    await screen.findByText("Coffee launch");
    expect(api.getCampaignCatalog).toHaveBeenCalledWith(expect.objectContaining({ page: 1, pageSize: 20, sort: "promoted", query: undefined }), expect.any(AbortSignal));
  });

  it("keeps draft filters local until Apply and discards them when the sheet closes", async () => {
    renderCampaigns();
    await screen.findByText("Coffee launch");
    const callsBeforeDraft = api.getCampaignCatalog.mock.calls.length;

    fireEvent.click(screen.getByRole("button", { name: translate("campaigns.filtersAria", undefined, "ru") }));
    fireEvent.change(screen.getByLabelText(translate("common.city", undefined, "ru")), { target: { value: "samarkand" } });
    expect(api.getCampaignCatalog).toHaveBeenCalledTimes(callsBeforeDraft);
    fireEvent.click(screen.getByRole("button", { name: "close-sheet" }));
    expect(api.getCampaignCatalog).toHaveBeenCalledTimes(callsBeforeDraft);

    fireEvent.click(screen.getByRole("button", { name: translate("campaigns.filtersAria", undefined, "ru") }));
    expect(screen.getByLabelText(translate("common.city", undefined, "ru"))).toHaveValue("");
  });

  it("applies filters once, renders removable chips, and resets the default catalog", async () => {
    renderCampaigns();
    await screen.findByText("Coffee launch");
    const initialCalls = api.getCampaignCatalog.mock.calls.length;

    fireEvent.click(screen.getByRole("button", { name: translate("campaigns.filtersAria", undefined, "ru") }));
    fireEvent.change(screen.getByLabelText(translate("common.city", undefined, "ru")), { target: { value: "samarkand" } });
    fireEvent.change(screen.getByLabelText(translate("common.categories", undefined, "ru")), { target: { value: "beauty" } });
    fireEvent.click(screen.getByRole("button", { name: translate("common.apply", undefined, "ru") }));

    await waitFor(() => expect(api.getCampaignCatalog).toHaveBeenCalledTimes(initialCalls + 1));
    expect(api.getCampaignCatalog).toHaveBeenLastCalledWith(expect.objectContaining({ city: "samarkand", category: "beauty" }), expect.any(AbortSignal));
    expect(screen.getAllByRole("button", { name: /Убрать фильтр/ })).toHaveLength(2);

    fireEvent.click(screen.getByRole("button", { name: translate("search.resetAll", undefined, "ru") }));
    await waitFor(() => expect(api.getCampaignCatalog).toHaveBeenCalledTimes(initialCalls + 2));
    expect(api.getCampaignCatalog.mock.calls.at(-1)?.[0]).toMatchObject({ sort: "promoted", page: 1, pageSize: 20, query: undefined });
    expect(api.getCampaignCatalog.mock.calls.at(-1)?.[0]).not.toHaveProperty("city");
    expect(api.getCampaignCatalog.mock.calls.at(-1)?.[0]).not.toHaveProperty("category");
  });

  it("uses active MarketplaceRole and a real Business profile for the create action", async () => {
    renderCampaigns();
    await screen.findByText("Coffee launch");
    await waitFor(() => expect(screen.getByRole("button", { name: translate("campaigns.createAria", undefined, "ru") })).toBeInTheDocument());

    cleanup();
    api.getCurrentPlatformUser.mockResolvedValue({ selectedMarketplaceRole: "Blogger" });
    renderCampaigns();
    await screen.findByText("Coffee launch");
    await waitFor(() => expect(screen.queryByRole("button", { name: translate("campaigns.createAria", undefined, "ru") })).not.toBeInTheDocument());
  });
});
