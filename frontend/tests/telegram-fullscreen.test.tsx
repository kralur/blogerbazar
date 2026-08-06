import { render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { I18nProvider } from "../src/i18n";
import { TelegramProvider } from "../src/telegram/TelegramProvider";

function renderTelegram(platform: string) {
  const webApp = {
    platform,
    colorScheme: "light" as const,
    expand: vi.fn(),
    ready: vi.fn(),
    requestFullscreen: vi.fn(),
    disableVerticalSwipes: vi.fn(),
    MainButton: { hide: vi.fn() },
    SettingsButton: { hide: vi.fn() }
  };
  window.Telegram = { WebApp: webApp };
  render(<I18nProvider><TelegramProvider><p>ready</p></TelegramProvider></I18nProvider>);
  return webApp;
}

describe("Telegram fullscreen", () => {
  afterEach(() => {
    delete window.Telegram;
  });

  it("requests fullscreen on supported mobile Telegram clients", async () => {
    const webApp = renderTelegram("ios");
    await screen.findByText("ready");
    expect(webApp.expand).toHaveBeenCalled();
    expect(webApp.requestFullscreen).toHaveBeenCalledTimes(1);
  });

  it("uses only expand on Telegram Desktop", async () => {
    const webApp = renderTelegram("tdesktop");
    await screen.findByText("ready");
    expect(webApp.expand).toHaveBeenCalled();
    expect(webApp.requestFullscreen).not.toHaveBeenCalled();
  });
});
