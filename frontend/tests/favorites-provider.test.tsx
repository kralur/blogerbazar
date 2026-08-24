import { act, cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider, translate } from "../src/i18n";

const api = vi.hoisted(() => ({ getCurrentPlatformUser: vi.fn(), getFavorites: vi.fn(), saveFavorite: vi.fn(), removeFavorite: vi.fn(), getBrandFaceFavorites: vi.fn(), saveBrandFaceFavorite: vi.fn(), removeBrandFaceFavorite: vi.fn() }));

vi.mock("../src/api/marketplace", async (importOriginal) => ({
  ...(await importOriginal<typeof import("../src/api/marketplace")>()),
  getCurrentPlatformUser: api.getCurrentPlatformUser,
  getFavorites: api.getFavorites,
  saveFavorite: api.saveFavorite,
  removeFavorite: api.removeFavorite,
  getBrandFaceFavorites: api.getBrandFaceFavorites,
  saveBrandFaceFavorite: api.saveBrandFaceFavorite,
  removeBrandFaceFavorite: api.removeBrandFaceFavorite
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
  return <><span>{ready ? "ready" : "loading"}</span><span>{isFavorite("blogger", "same") ? "blogger-saved" : "blogger-unsaved"}</span><span>{isFavorite("brandFace", "same") ? "brand-face-saved" : "brand-face-unsaved"}</span><button onClick={() => void toggleFavorite("blogger", "same")} type="button">toggle-blogger</button><button onClick={() => void toggleFavorite("brandFace", "same")} type="button">toggle-brand-face</button><button onClick={() => void refreshFavorites()} type="button">refresh</button></>;
}

describe("FavoritesProvider", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    api.getCurrentPlatformUser.mockResolvedValue({ selectedMarketplaceRole: 2 });
    api.getFavorites.mockResolvedValue({ items: [], total: 0, page: 1, pageSize: 50 });
    api.getBrandFaceFavorites.mockResolvedValue({ items: [], total: 0, page: 1, pageSize: 50, hasMore: false });
    api.saveFavorite.mockResolvedValue(undefined);
    api.removeFavorite.mockResolvedValue(undefined);
    api.saveBrandFaceFavorite.mockResolvedValue(undefined);
    api.removeBrandFaceFavorite.mockResolvedValue(undefined);
  });

  afterEach(() => cleanup());

  it("optimistically saves a Blogger favorite and exposes selected accessibility state", async () => {
    const user = userEvent.setup();
    renderFavorites(<FavoriteButton bloggerId="blogger" />);
    const button = await screen.findByRole("button", { name: translate("favorites.saveAria", undefined, "ru") });
    await user.click(button);
    await waitFor(() => expect(api.saveFavorite).toHaveBeenCalledWith("blogger"));
    expect(screen.getByRole("button", { name: translate("favorites.removeAria", undefined, "ru") })).toHaveAttribute("aria-pressed", "true");
  });

  it("shows the Brand Face action only to Business and uses its typed endpoint", async () => {
    const user = userEvent.setup();
    renderFavorites(<FavoriteButton brandFaceId="face" />);
    const button = await screen.findByRole("button", { name: translate("favorites.saveBrandFaceAria", undefined, "ru") });
    await user.click(button);
    await waitFor(() => expect(api.saveBrandFaceFavorite).toHaveBeenCalledWith("face"));
    expect(screen.getByRole("button", { name: translate("favorites.removeBrandFaceAria", undefined, "ru") })).toHaveAttribute("aria-pressed", "true");
  });

  it.each([0, 1])("hides the Brand Face action for non-Business role %s", async (role) => {
    api.getCurrentPlatformUser.mockResolvedValue({ selectedMarketplaceRole: role });
    renderFavorites(<FavoriteButton brandFaceId="face" />);
    await waitFor(() => expect(api.getCurrentPlatformUser).toHaveBeenCalled());
    expect(screen.queryByRole("button", { name: translate("favorites.saveBrandFaceAria", undefined, "ru") })).not.toBeInTheDocument();
    expect(api.getBrandFaceFavorites).not.toHaveBeenCalled();
  });

  it("rolls back an optimistic Brand Face favorite and shows localized feedback", async () => {
    api.saveBrandFaceFavorite.mockRejectedValueOnce(new Error("failed"));
    const user = userEvent.setup();
    renderFavorites(<FavoriteButton brandFaceId="face" />);
    await user.click(await screen.findByRole("button", { name: translate("favorites.saveBrandFaceAria", undefined, "ru") }));
    expect(await screen.findByRole("alert")).toHaveTextContent(translate("favorites.actionFailed", undefined, "ru"));
    expect(screen.getByRole("button", { name: translate("favorites.saveBrandFaceAria", undefined, "ru") })).toHaveAttribute("aria-pressed", "false");
  });

  it("keeps Blogger and Brand Face keys distinct when UUID values collide", async () => {
    api.getFavorites.mockResolvedValue({ items: [{ bloggerId: "same" }], total: 1, page: 1, pageSize: 50 });
    api.getBrandFaceFavorites.mockResolvedValue({ items: [{ id: "same" }], total: 1, page: 1, pageSize: 50, hasMore: false });
    renderFavorites(<Probe />);
    expect(await screen.findByText("blogger-saved")).toBeInTheDocument();
    expect(screen.getByText("brand-face-saved")).toBeInTheDocument();
  });

  it("does not allow a late refresh to overwrite a newer typed optimistic mutation", async () => {
    const user = userEvent.setup();
    renderFavorites(<Probe />);
    await screen.findByText("ready");
    let resolveRefresh!: (value: { items: Array<{ id: string }>; total: number; page: number; pageSize: number; hasMore: boolean }) => void;
    api.getBrandFaceFavorites.mockImplementationOnce(() => new Promise((resolve) => { resolveRefresh = resolve; }));
    await user.click(screen.getByRole("button", { name: "refresh" }));
    await user.click(screen.getByRole("button", { name: "toggle-brand-face" }));
    expect(screen.getByText("brand-face-saved")).toBeInTheDocument();
    await act(async () => resolveRefresh({ items: [], total: 0, page: 1, pageSize: 50, hasMore: false }));
    expect(screen.getByText("brand-face-saved")).toBeInTheDocument();
  });
});
