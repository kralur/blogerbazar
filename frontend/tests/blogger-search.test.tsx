import { act, cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider, translate } from "../src/i18n";

const api = vi.hoisted(() => ({ getBloggers: vi.fn(), getCategories: vi.fn() }));
let observerCallback: ((entries: Array<{ isIntersecting: boolean }>) => void) | undefined;

vi.mock("../src/api/marketplace", async (importOriginal) => ({
  ...(await importOriginal<typeof import("../src/api/marketplace")>()),
  getBloggers: api.getBloggers,
  getCategories: api.getCategories
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
  Skeleton: () => <div data-testid="skeleton" />
}));

import { BloggerSearch } from "../src/pages/BloggerSearch";

const firstPage = { bloggers: [{ id: "a", name: "Amina", city: "tashkent", categories: ["beauty"], totalFollowers: 1000, reviewsCount: 0, completedDealsCount: 0 }], total: 1, page: 1, pageSize: 20 };
const secondPage = { bloggers: [{ id: "a", name: "Amina", city: "tashkent", categories: ["beauty"], totalFollowers: 1000, reviewsCount: 0, completedDealsCount: 0 }, { id: "b", name: "Bek", city: "samarkand", categories: ["food"], totalFollowers: 2000, reviewsCount: 0, completedDealsCount: 0 }], total: 40, page: 2, pageSize: 20 };

function renderSearch() {
  return render(<I18nProvider><BloggerSearch /></I18nProvider>);
}

describe("BloggerSearch", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    api.getCategories.mockResolvedValue(["beauty", "food"]);
    api.getBloggers.mockResolvedValue(firstPage);
    observerCallback = undefined;
    vi.stubGlobal("IntersectionObserver", class {
      constructor(callback: (entries: Array<{ isIntersecting: boolean }>) => void) { observerCallback = callback; }
      observe() {}
      disconnect() {}
    });
    Object.defineProperty(window.navigator, "onLine", { configurable: true, value: true });
  });

  afterEach(() => cleanup());

  it("shows initial skeletons then a truthful count and end-of-list state", async () => {
    renderSearch();
    expect(screen.getAllByTestId("skeleton")).toHaveLength(3);
    expect(await screen.findByText("Amina")).toBeInTheDocument();
    expect(screen.getByText(translate("search.found", { count: 1 }, "ru"))).toBeInTheDocument();
    expect(screen.getByText(translate("search.endOfList", undefined, "ru"))).toBeInTheDocument();
  });

  it("keeps draft filters local until Apply and discards them on close", async () => {
    const user = userEvent.setup();
    renderSearch();
    await screen.findByText("Amina");
    const callsBeforeDraft = api.getBloggers.mock.calls.length;
    await user.click(screen.getByRole("button", { name: translate("search.filters", undefined, "ru") }));
    fireEvent.change(screen.getByLabelText(translate("common.city", undefined, "ru")), { target: { value: "tashkent-city" } });
    expect(api.getBloggers).toHaveBeenCalledTimes(callsBeforeDraft);
    await user.click(screen.getByRole("button", { name: "close" }));
    expect(screen.queryByText(translate("search.activeFilters", undefined, "ru"))).not.toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: translate("search.filters", undefined, "ru") }));
    fireEvent.change(screen.getByLabelText(translate("common.city", undefined, "ru")), { target: { value: "tashkent-city" } });
    await user.click(screen.getByRole("button", { name: translate("common.apply", undefined, "ru") }));
    await waitFor(() => expect(api.getBloggers.mock.calls.some(([filters]) => filters.city === "tashkent-city" && filters.page === 1)).toBe(true));
    expect(screen.getByRole("group", { name: translate("search.activeFilters", undefined, "ru") })).toBeInTheDocument();
  });

  it("resets all filters and removes individual active chips", async () => {
    const user = userEvent.setup();
    renderSearch();
    await screen.findByText("Amina");
    await user.click(screen.getByRole("button", { name: translate("search.filters", undefined, "ru") }));
    fireEvent.change(screen.getByLabelText(translate("common.categories", undefined, "ru")), { target: { value: "beauty" } });
    await user.click(screen.getByRole("button", { name: translate("common.apply", undefined, "ru") }));
    await screen.findByRole("button", { name: translate("search.removeFilterAria", { filter: translate("taxonomy.category.beauty", undefined, "ru") }, "ru") });
    await user.click(screen.getByRole("button", { name: translate("search.removeFilterAria", { filter: translate("taxonomy.category.beauty", undefined, "ru") }, "ru") }));
    await waitFor(() => expect(api.getBloggers.mock.calls.some(([filters]) => filters.category === undefined && filters.page === 1)).toBe(true));

    await user.click(screen.getByRole("button", { name: translate("search.filters", undefined, "ru") }));
    fireEvent.change(screen.getByLabelText(translate("common.city", undefined, "ru")), { target: { value: "tashkent-city" } });
    await user.click(screen.getByRole("button", { name: translate("common.apply", undefined, "ru") }));
    await user.click(screen.getByRole("button", { name: translate("search.resetAll", undefined, "ru") }));
    await waitFor(() => expect(api.getBloggers.mock.calls.some(([filters]) => filters.city === undefined && filters.sort === "popular" && filters.page === 1)).toBe(true));
  });

  it("debounces query changes and ignores an older response", async () => {
    vi.useFakeTimers();
    try {
      let resolveFirst!: (value: typeof firstPage) => void;
      let resolveSecond!: (value: typeof firstPage) => void;
      api.getBloggers.mockImplementationOnce(() => new Promise<typeof firstPage>((resolve) => { resolveFirst = resolve; })).mockImplementationOnce(() => new Promise<typeof firstPage>((resolve) => { resolveSecond = resolve; }));
      renderSearch();
      fireEvent.change(screen.getByLabelText(translate("search.placeholder", undefined, "ru")), { target: { value: "fresh" } });
      await act(async () => { await vi.advanceTimersByTimeAsync(300); });
      expect(api.getBloggers).toHaveBeenCalledTimes(2);
      await act(async () => resolveSecond({ ...firstPage, bloggers: [{ ...firstPage.bloggers[0], name: "Fresh" }] }));
      expect(screen.getByText("Fresh")).toBeInTheDocument();
      await act(async () => resolveFirst(firstPage));
      expect(screen.queryByText("Amina")).not.toBeInTheDocument();
    } finally {
      vi.useRealTimers();
    }
  });

  it("updates a cached Search instance from a new hash category", async () => {
    renderSearch();
    await screen.findByText("Amina");
    window.location.hash = "#/";
    window.location.hash = "#/search?category=beauty";
    window.dispatchEvent(new HashChangeEvent("hashchange"));
    await waitFor(() => expect(api.getBloggers.mock.calls.some(([filters]) => filters.category === "beauty" && filters.page === 1)).toBe(true));
  });

  it("deduplicates the next page and does not request it twice", async () => {
    api.getBloggers.mockResolvedValueOnce({ ...firstPage, total: 40 }).mockResolvedValueOnce(secondPage);
    renderSearch();
    await screen.findByText("Amina");
    await act(async () => observerCallback?.([{ isIntersecting: true }]));
    await screen.findByText("Bek");
    expect(screen.getAllByText("Amina")).toHaveLength(1);
    expect(api.getBloggers).toHaveBeenCalledTimes(2);
  });

  it("shows offline and retry states without treating taxonomy failure as catalog failure", async () => {
    Object.defineProperty(window.navigator, "onLine", { configurable: true, value: false });
    api.getCategories.mockRejectedValue(new Error("taxonomy unavailable"));
    api.getBloggers.mockRejectedValueOnce(new Error("offline")).mockResolvedValueOnce(firstPage);
    const user = userEvent.setup();
    renderSearch();
    expect(await screen.findByText(translate("ui.offlineTitle", undefined, "ru"))).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: translate("common.retry", undefined, "ru") }));
    expect(await screen.findByText("Amina")).toBeInTheDocument();
  });
});
