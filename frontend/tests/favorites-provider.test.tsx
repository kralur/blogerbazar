import { act, cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider, translate } from "../src/i18n";

const api = vi.hoisted(() => ({ getCurrentPlatformUser: vi.fn(), getFavorites: vi.fn(), saveFavorite: vi.fn(), removeFavorite: vi.fn() }));

vi.mock("../src/api/marketplace", async (importOriginal) => ({
  ...(await importOriginal<typeof import("../src/api/marketplace")>()),
  getCurrentPlatformUser: api.getCurrentPlatformUser,
  getFavorites: api.getFavorites,
  saveFavorite: api.saveFavorite,
  removeFavorite: api.removeFavorite
}));
vi.mock("../src/telegram/TelegramProvider", () => ({ useTelegram: () => ({ haptic: { success: vi.fn(), error: vi.fn() } }) }));
vi.mock("../src/components/ui", () => ({ Icon: () => <svg />, Toast: ({ message }: { message: string }) => message ? <div role="alert">{message}</div> : null }));

import { FavoriteButton } from "../src/components/FavoriteButton";
import { FavoritesProvider, useFavorites } from "../src/features/favorites/FavoritesProvider";

function renderFavorites(children: React.ReactNode) {
  return render(<I18nProvider><FavoritesProvider>{children}</FavoritesProvider></I18nProvider>);
}

function Probe() {
  const { ready, isFavorite, toggleFavorite, refreshFavorites } = useFavorites();
  return <><span>{ready ? "ready" : "loading"}</span><span>{isFavorite("blogger") ? "saved" : "unsaved"}</span><button onClick={() => void toggleFavorite("blogger")} type="button">toggle</button><button onClick={() => void refreshFavorites()} type="button">refresh</button></>;
}

describe("FavoritesProvider", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    api.getCurrentPlatformUser.mockResolvedValue({ selectedMarketplaceRole: 2 });
    api.getFavorites.mockResolvedValue({ items: [], total: 0, page: 1, pageSize: 50 });
    api.saveFavorite.mockResolvedValue(undefined);
    api.removeFavorite.mockResolvedValue(undefined);
  });

  afterEach(() => cleanup());

  it("optimistically saves a favorite and exposes selected accessibility state", async () => {
    const user = userEvent.setup();
    renderFavorites(<FavoriteButton bloggerId="blogger" />);
    const button = await screen.findByRole("button", { name: translate("favorites.saveAria", undefined, "ru") });
    expect(button).toHaveClass("catalog-favorite-button");
    await user.click(button);
    await waitFor(() => expect(api.saveFavorite).toHaveBeenCalledWith("blogger"));
    expect(screen.getByRole("button", { name: translate("favorites.removeAria", undefined, "ru") })).toHaveAttribute("aria-pressed", "true");
  });

  it("rolls back an optimistic favorite and shows visible localized feedback", async () => {
    api.saveFavorite.mockRejectedValueOnce(new Error("failed"));
    const user = userEvent.setup();
    renderFavorites(<FavoriteButton bloggerId="blogger" />);
    await user.click(await screen.findByRole("button", { name: translate("favorites.saveAria", undefined, "ru") }));
    expect(await screen.findByRole("alert")).toHaveTextContent(translate("favorites.actionFailed", undefined, "ru"));
    expect(screen.getByRole("button", { name: translate("favorites.saveAria", undefined, "ru") })).toHaveAttribute("aria-pressed", "false");
  });

  it("does not allow a late refresh to overwrite a newer optimistic mutation", async () => {
    const user = userEvent.setup();
    renderFavorites(<Probe />);
    await screen.findByText("ready");
    let resolveRefresh!: (value: { items: Array<{ bloggerId: string }>; total: number; page: number; pageSize: number }) => void;
    api.getFavorites.mockImplementationOnce(() => new Promise((resolve) => { resolveRefresh = resolve; }));
    await user.click(screen.getByRole("button", { name: "refresh" }));
    await user.click(screen.getByRole("button", { name: "toggle" }));
    expect(screen.getByText("saved")).toBeInTheDocument();
    await act(async () => resolveRefresh({ items: [], total: 0, page: 1, pageSize: 50 }));
    expect(screen.getByText("saved")).toBeInTheDocument();
  });
});
