import { act, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { I18nProvider, translate } from "../src/i18n";
import { getThemePreference, resolveColorScheme, setThemePreference } from "../src/lib/themePreference";
import { TelegramProvider } from "../src/telegram/TelegramProvider";

vi.mock("../src/components/AccountActions", () => ({ AccountActions: () => null }));
vi.mock("../src/components/ui", async (importOriginal) => ({ ...(await importOriginal<typeof import("../src/components/ui")>()), BottomNav: () => null }));

import { Settings } from "../src/pages/Settings";

const ru = (key: string) => translate(key, undefined, "ru");

describe("Theme preference", () => {
  afterEach(() => {
    localStorage.clear();
    delete window.Telegram;
  });

  it("follows Telegram by default and lets the user override it", () => {
    expect(getThemePreference()).toBe("telegram");
    expect(resolveColorScheme("telegram", "dark")).toBe("dark");
    expect(resolveColorScheme("light", "dark")).toBe("light");
    expect(resolveColorScheme("dark", "light")).toBe("dark");
  });

  it("applies the chosen theme over the Telegram theme at once", async () => {
    window.Telegram = { WebApp: { colorScheme: "light", ready: vi.fn(), expand: vi.fn(), MainButton: { hide: vi.fn() }, SettingsButton: { hide: vi.fn() } } };
    render(<I18nProvider><TelegramProvider><Settings /></TelegramProvider></I18nProvider>);
    await screen.findByRole("group", { name: ru("settings.theme") });
    expect(document.documentElement.dataset.telegramTheme).toBe("light");

    fireEvent.click(screen.getByRole("button", { name: ru("settings.themeDark") }));
    expect(localStorage.getItem("bloggerbazar.theme")).toBe("dark");
    expect(document.documentElement.dataset.telegramTheme).toBe("dark");

    fireEvent.click(screen.getByRole("button", { name: ru("settings.themeTelegram") }));
    expect(localStorage.getItem("bloggerbazar.theme")).toBeNull();
    expect(document.documentElement.dataset.telegramTheme).toBe("light");
  });

  it("ignores a stored value it does not know", () => {
    localStorage.setItem("bloggerbazar.theme", "sepia");
    expect(getThemePreference()).toBe("telegram");
    act(() => setThemePreference("light"));
    expect(getThemePreference()).toBe("light");
  });
});
