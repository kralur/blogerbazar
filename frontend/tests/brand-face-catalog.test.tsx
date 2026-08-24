import { act, cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider, translate } from "../src/i18n";

const api = vi.hoisted(() => ({ getBloggers: vi.fn(), getCategories: vi.fn(), getBrandFaceCatalog: vi.fn() }));
let observerCallback: ((entries: Array<{ isIntersecting: boolean }>) => void) | undefined;

vi.mock("../src/api/marketplace", async (importOriginal) => ({
  ...(await importOriginal<typeof import("../src/api/marketplace")>()),
  getBloggers: api.getBloggers,
  getCategories: api.getCategories,
  getBrandFaceCatalog: api.getBrandFaceCatalog
}));
vi.mock("../src/hooks/useScrollRestoration", () => ({ useScrollRestoration: vi.fn() }));
vi.mock("../src/hooks/useProfileDataRefresh", () => ({ useProfileDataRefresh: vi.fn() }));
vi.mock("../src/telegram/TelegramProvider", () => ({ useTelegram: () => ({ haptic: { selection: vi.fn() } }) }));
vi.mock("../src/components/LanguageSwitcher", () => ({ LanguageSwitcher: () => <span>language</span> }));
vi.mock("../src/components/BloggerCard", () => ({ BloggerCard: ({ blogger }: { blogger: { name: string } }) => <article>{blogger.name}</article> }));
vi.mock("../src/components/ui", () => ({
  BottomNav: () => <nav aria-label="bottom-nav" />,
  BottomSheet: ({ open, title, id, children, onClose }: { open: boolean; title: string; id?: string; children: React.ReactNode; onClose: () => void }) => open ? <section aria-label={title} id={id}><button onClick={onClose} type="button">close</button>{children}</section> : null,
  Icon: () => <svg />,
  SearchBar: ({ placeholder, value, onChange, className }: { placeholder?: string; value?: string; onChange?: React.ChangeEventHandler<HTMLInputElement>; className?: string }) => <input aria-label={placeholder} className={className} onChange={onChange} value={value} />,
  Skeleton: () => <div data-testid="skeleton" />,
  Avatar: ({ name }: { name: string }) => <span>{name.slice(0, 1)}</span>
}));

import { BloggerSearch } from "../src/pages/BloggerSearch";

const bloggerPage = { bloggers: [{ id: "blogger-a", name: "Amina", city: "tashkent", categories: ["beauty"], totalFollowers: 1_000, reviewsCount: 0, completedDealsCount: 0 }], total: 1, page: 1, pageSize: 20 };
const brandFacePage = { items: [{ id: "face-a", name: "Dilnoza", city: "tashkent-city", categories: ["beauty"], languages: ["Русский"], collaborationPrice: 250_000, avatarUrl: null, isPromoted: true, createdAtUtc: "2026-08-01T00:00:00Z" }], total: 1, page: 1, pageSize: 20, hasMore: false };

function renderSearch(hash = "#/search") {
  window.location.hash = hash;
  return render(<I18nProvider><BloggerSearch /></I18nProvider>);
}

describe("Brand Face catalog in Search", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    api.getCategories.mockResolvedValue(["beauty", "food"]);
    api.getBloggers.mockResolvedValue(bloggerPage);
    api.getBrandFaceCatalog.mockResolvedValue(brandFacePage);
    observerCallback = undefined;
    vi.stubGlobal("IntersectionObserver", class {
      constructor(callback: (entries: Array<{ isIntersecting: boolean }>) => void) { observerCallback = callback; }
      observe() {}
      disconnect() {}
    });
    Object.defineProperty(window.navigator, "onLine", { configurable: true, value: true });
  });

  afterEach(() => cleanup());

  it("defaults to Blogger and normalizes an unknown type", async () => {
    renderSearch("#/search?type=unknown");
    expect(await screen.findByText("Amina")).toBeInTheDocument();
    expect(api.getBrandFaceCatalog).not.toHaveBeenCalled();
    expect(screen.getByRole("button", { name: translate("search.typeBloggers", undefined, "ru") })).toHaveAttribute("aria-pressed", "true");
  });

  it("loads Brand Face catalog from the typed route and renders only supported card fields", async () => {
    renderSearch("#/search?type=brand-face");
    expect(await screen.findByText("Dilnoza")).toBeInTheDocument();
    expect(api.getBrandFaceCatalog).toHaveBeenCalledTimes(1);
    expect(api.getBrandFaceCatalog).toHaveBeenCalledWith(expect.objectContaining({ page: 1, pageSize: 20, sort: "promoted" }), expect.any(AbortSignal));
    expect(screen.getByText(translate("search.promoted", undefined, "ru"))).toBeInTheDocument();
    expect(screen.getByText((_, element) => element?.textContent?.replace(/\u00a0/g, " ") === "250 000 сум")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /favorite/i })).not.toBeInTheDocument();
    expect(screen.getByRole("link", { name: translate("search.openBrandFaceAria", { name: "Dilnoza" }, "ru") })).toHaveAttribute("href", "#/brand-face-detail/face-a");
  });

  it("keeps Blogger and Brand Face query state separate while switching segments", async () => {
    const user = userEvent.setup();
    renderSearch();
    await screen.findByText("Amina");
    fireEvent.change(screen.getAllByLabelText(translate("search.placeholder", undefined, "ru"))[0], { target: { value: "Amina" } });
    await user.click(screen.getByRole("button", { name: translate("search.typeBrandFaces", undefined, "ru") }));
    expect(window.location.hash).toBe("#/search?type=brand-face");
    await screen.findByText("Dilnoza");
    await user.click(screen.getByRole("button", { name: translate("search.typeBloggers", undefined, "ru") }));
    expect(screen.getAllByLabelText(translate("search.placeholder", undefined, "ru"))[0]).toHaveValue("Amina");
  });

  it("debounces Brand Face query changes before requesting a fresh first page", async () => {
    vi.useFakeTimers();
    try {
      renderSearch("#/search?type=brand-face");
      await act(async () => { await vi.runAllTimersAsync(); });
      api.getBrandFaceCatalog.mockClear();
      const brandFaceInput = screen.getAllByLabelText(translate("search.brandFacePlaceholder", undefined, "ru")).at(-1);
      fireEvent.change(brandFaceInput!, { target: { value: "Dilnoza" } });
      await act(async () => { await vi.advanceTimersByTimeAsync(299); });
      expect(api.getBrandFaceCatalog).not.toHaveBeenCalled();
      await act(async () => { await vi.advanceTimersByTimeAsync(1); });
      expect(api.getBrandFaceCatalog).toHaveBeenCalledWith(expect.objectContaining({ query: "Dilnoza", page: 1 }), expect.any(AbortSignal));
    } finally {
      vi.useRealTimers();
    }
  });

  it("keeps Brand Face draft filters local until Apply, then resets only Brand Face", async () => {
    const user = userEvent.setup();
    renderSearch("#/search?type=brand-face");
    await screen.findByText("Dilnoza");
    const callsBeforeDraft = api.getBrandFaceCatalog.mock.calls.length;
    await user.click(screen.getByRole("button", { name: translate("search.filters", undefined, "ru") }));
    fireEvent.change(screen.getByLabelText(translate("common.city", undefined, "ru")), { target: { value: "tashkent-city" } });
    expect(api.getBrandFaceCatalog).toHaveBeenCalledTimes(callsBeforeDraft);
    await user.click(screen.getByRole("button", { name: "close" }));
    expect(screen.queryByRole("group", { name: translate("search.activeFilters", undefined, "ru") })).not.toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: translate("search.filters", undefined, "ru") }));
    fireEvent.change(screen.getByLabelText(translate("common.city", undefined, "ru")), { target: { value: "tashkent-city" } });
    await user.click(screen.getByRole("button", { name: translate("common.apply", undefined, "ru") }));
    await waitFor(() => expect(api.getBrandFaceCatalog.mock.calls.some(([filters]) => filters.city === "tashkent-city")).toBe(true));
    expect(screen.getByRole("group", { name: translate("search.activeFilters", undefined, "ru") })).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: translate("search.resetAll", undefined, "ru") }));
    await waitFor(() => expect(api.getBrandFaceCatalog.mock.calls.some(([filters]) => filters.city === undefined && filters.sort === "promoted")).toBe(true));
    await user.click(screen.getByRole("button", { name: translate("search.filters", undefined, "ru") }));
    fireEvent.change(screen.getByLabelText(translate("common.city", undefined, "ru")), { target: { value: "tashkent-city" } });
    await user.click(screen.getByRole("button", { name: translate("common.apply", undefined, "ru") }));
    await waitFor(() => expect(screen.getByRole("group", { name: translate("search.activeFilters", undefined, "ru") })).toBeInTheDocument());
    await user.click(screen.getByRole("button", { name: translate("search.removeFilterAria", { filter: translate("taxonomy.city.tashkent-city", undefined, "ru") }, "ru") }));
    await waitFor(() => expect(screen.queryByRole("group", { name: translate("search.activeFilters", undefined, "ru") })).not.toBeInTheDocument());
  });

  it("applies an explicit Home category only to Brand Face and deduplicates appended pages", async () => {
    api.getBrandFaceCatalog.mockResolvedValueOnce({ ...brandFacePage, total: 40, hasMore: true }).mockResolvedValueOnce({ ...brandFacePage, items: [...brandFacePage.items, { ...brandFacePage.items[0], id: "face-b", name: "Madina" }], total: 40, page: 2, hasMore: false });
    renderSearch("#/search?type=brand-face&category=beauty");
    await screen.findByText("Dilnoza");
    expect(api.getBrandFaceCatalog.mock.calls.some(([filters]) => filters.category === "beauty")).toBe(true);
    await act(async () => observerCallback?.([{ isIntersecting: true }]));
    expect(await screen.findByText("Madina")).toBeInTheDocument();
    expect(screen.getAllByText("Dilnoza")).toHaveLength(1);
  });

  it("does not let a stale Blogger request overwrite the active Brand Face catalog", async () => {
    let resolveBlogger!: (value: typeof bloggerPage) => void;
    api.getBloggers.mockImplementationOnce(() => new Promise<typeof bloggerPage>((resolve) => { resolveBlogger = resolve; }));
    const user = userEvent.setup();
    renderSearch();
    await user.click(screen.getByRole("button", { name: translate("search.typeBrandFaces", undefined, "ru") }));
    await screen.findByText("Dilnoza");
    await act(async () => resolveBlogger(bloggerPage));
    const activeCatalog = document.querySelector(".catalog-search:not([hidden])");
    expect(activeCatalog).toHaveTextContent("Dilnoza");
    expect(activeCatalog).not.toHaveTextContent("Amina");
  });

  it("shows a localized missing price instead of a fake zero", async () => {
    api.getBrandFaceCatalog.mockResolvedValue({ ...brandFacePage, items: [{ ...brandFacePage.items[0], collaborationPrice: null, isPromoted: false }] });
    renderSearch("#/search?type=brand-face");
    expect(await screen.findByText(translate("search.priceNotSpecified", undefined, "ru"))).toBeInTheDocument();
    expect(screen.queryByText(/0 сум/)).not.toBeInTheDocument();
  });

  it("distinguishes initial, empty, offline, and server retry states", async () => {
    let resolveCatalog!: (value: typeof brandFacePage) => void;
    api.getBrandFaceCatalog.mockImplementationOnce(() => new Promise<typeof brandFacePage>((resolve) => { resolveCatalog = resolve; }));
    const { unmount } = renderSearch("#/search?type=brand-face");
    expect(screen.getAllByTestId("skeleton")).toHaveLength(3);
    await act(async () => resolveCatalog({ ...brandFacePage, items: [], total: 0 }));
    expect(await screen.findByText(translate("search.brandFaceEmptyTitle", undefined, "ru"))).toBeInTheDocument();
    unmount();

    Object.defineProperty(window.navigator, "onLine", { configurable: true, value: false });
    api.getBrandFaceCatalog.mockRejectedValueOnce(new Error("offline")).mockResolvedValueOnce(brandFacePage);
    const user = userEvent.setup();
    renderSearch("#/search?type=brand-face");
    expect(await screen.findByText(translate("ui.offlineTitle", undefined, "ru"))).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: translate("common.retry", undefined, "ru") }));
    expect(await screen.findByText("Dilnoza")).toBeInTheDocument();
  });
});
