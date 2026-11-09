import { render, renderHook, screen, waitFor } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { I18nProvider, translate } from "../src/i18n";

const api = vi.hoisted(() => ({ getMyBusinessProfile: vi.fn() }));
vi.mock("../src/api/marketplace", async (importOriginal) => ({ ...(await importOriginal<typeof import("../src/api/marketplace")>()), ...api }));
vi.mock("../src/features/actionCounts/ActionCountsProvider", () => ({ useActionCounts: () => ({ total: 2 }), ActionBadge: ({ count }: { count: number }) => <span>{count}</span> }));

import { DesktopNav } from "../src/components/DesktopNav";
import { useDesktopLayout } from "../src/lib/desktopLayout";

const ru = (key: string) => translate(key, undefined, "ru");
function mockScreen(wide: boolean) {
  window.matchMedia = vi.fn().mockImplementation((query: string) => ({ matches: wide, media: query, addEventListener: vi.fn(), removeEventListener: vi.fn() }));
}

describe("website layout on a computer (D64 stage B2)", () => {
  afterEach(() => { delete document.documentElement.dataset.desktop; });

  it("turns on for a wide browser window outside Telegram only", () => {
    mockScreen(true);
    const outside = renderHook(() => useDesktopLayout(true));
    expect(outside.result.current).toBe(true);
    expect(document.documentElement.dataset.desktop).toBe("true");
    outside.unmount();

    const telegram = renderHook(() => useDesktopLayout(false));
    expect(telegram.result.current).toBe(false);
    expect(document.documentElement.dataset.desktop).toBeUndefined();
    telegram.unmount();

    mockScreen(false);
    const narrow = renderHook(() => useDesktopLayout(true));
    expect(narrow.result.current).toBe(false);
    expect(document.documentElement.dataset.desktop).toBeUndefined();
  });

  it("shows the side menu with the company and marks the open section", async () => {
    api.getMyBusinessProfile.mockResolvedValue({ name: "Kofe Hona" });
    window.location.hash = "#/deal/42";
    render(<I18nProvider><DesktopNav /></I18nProvider>);

    expect(await screen.findByText("Kofe Hona")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: new RegExp(ru("nav.requests")) })).toHaveAttribute("aria-current", "page");
    expect(screen.getByRole("link", { name: ru("nav.home") })).not.toHaveAttribute("aria-current");
    await waitFor(() => expect(screen.getByRole("link", { name: ru("profile.settings") })).toHaveAttribute("href", "#/settings"));
  });
});
