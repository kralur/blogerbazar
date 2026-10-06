import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { I18nProvider } from "../src/i18n";

const favorites = vi.hoisted(() => ({ ownId: "blogger-own" }));
vi.mock("../src/features/favorites/FavoritesProvider", () => ({
  useFavorites: () => ({
    ready: true,
    canManageFavorite: () => true,
    isFavorite: () => false,
    isOwnProfile: (_target: string, id: string) => id === favorites.ownId,
    toggleFavorite: vi.fn()
  })
}));
vi.mock("../src/telegram/TelegramProvider", () => ({ useTelegram: () => ({ haptic: { success: vi.fn() } }) }));

import { FavoriteButton } from "../src/components/FavoriteButton";

describe("FavoriteButton", () => {
  it("is not offered on the user's own profile", () => {
    const { rerender } = render(<I18nProvider><FavoriteButton bloggerId="blogger-own" /></I18nProvider>);
    expect(screen.queryByRole("button")).not.toBeInTheDocument();

    rerender(<I18nProvider><FavoriteButton bloggerId="blogger-other" /></I18nProvider>);
    expect(screen.getByRole("button")).toBeInTheDocument();
  });
});
