import { render, screen } from "@testing-library/react";
import { useCallback, useEffect, useState } from "react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { Modal } from "../src/components/ui";
import { I18nProvider } from "../src/i18n";
import { isMobileTelegram, TelegramProvider, useTelegram } from "../src/telegram/TelegramProvider";
import { resolveTelegramContentTop } from "../src/telegram/telegramTheme";

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

  it("updates the shared content-safe inset after a Telegram viewport event", async () => {
    const handlers = new Map<string, () => void>();
    const webApp = {
      platform: "ios",
      colorScheme: "light" as const,
      contentSafeAreaInset: { top: 56, bottom: 20 },
      safeAreaInset: { top: 24, bottom: 12 },
      expand: vi.fn(),
      ready: vi.fn(),
      requestFullscreen: vi.fn(),
      disableVerticalSwipes: vi.fn(),
      MainButton: { hide: vi.fn() },
      SettingsButton: { hide: vi.fn() },
      onEvent: vi.fn((event: string, handler: () => void) => handlers.set(event, handler)),
      offEvent: vi.fn()
    };
    window.Telegram = { WebApp: webApp };
    render(<I18nProvider><TelegramProvider><p>ready</p></TelegramProvider></I18nProvider>);
    await screen.findByText("ready");
    expect(document.documentElement.style.getPropertyValue("--tg-content-safe-top")).toBe("80px");
    expect(document.documentElement.style.getPropertyValue("--tg-effective-content-top")).toBe("88px");

    webApp.contentSafeAreaInset.top = 72;
    handlers.get("contentSafeAreaChanged")?.();
    expect(document.documentElement.style.getPropertyValue("--tg-content-safe-top")).toBe("96px");
    expect(document.documentElement.style.getPropertyValue("--tg-effective-content-top")).toBe("104px");

    webApp.contentSafeAreaInset.top = 80;
    handlers.get("fullscreenChanged")?.();
    expect(document.documentElement.style.getPropertyValue("--tg-content-safe-top")).toBe("104px");
    expect(document.documentElement.style.getPropertyValue("--tg-effective-content-top")).toBe("112px");
  });

  it("uses the centralized Telegram-only content inset fallback until a client reports its value", async () => {
    const webApp = {
      platform: "ios",
      colorScheme: "light" as const,
      contentSafeAreaInset: { top: 0, bottom: 0 },
      safeAreaInset: { top: 0, bottom: 0 },
      expand: vi.fn(),
      ready: vi.fn(),
      requestFullscreen: vi.fn(),
      disableVerticalSwipes: vi.fn(),
      MainButton: { hide: vi.fn() },
      SettingsButton: { hide: vi.fn() }
    };
    window.Telegram = { WebApp: webApp };
    render(<I18nProvider><TelegramProvider><p>ready</p></TelegramProvider></I18nProvider>);
    await screen.findByText("ready");
    expect(document.documentElement.style.getPropertyValue("--tg-content-safe-top")).toBe("80px");
    expect(document.documentElement.style.getPropertyValue("--tg-effective-content-top")).toBe("88px");
  });

  it("leaves no empty band above the page in desktop Telegram", async () => {
    window.Telegram = { WebApp: {
      platform: "tdesktop",
      colorScheme: "light" as const,
      contentSafeAreaInset: { top: 0, bottom: 0 },
      safeAreaInset: { top: 0, bottom: 0 },
      expand: vi.fn(),
      ready: vi.fn(),
      requestFullscreen: vi.fn(),
      disableVerticalSwipes: vi.fn(),
      MainButton: { hide: vi.fn() },
      SettingsButton: { hide: vi.fn() }
    } };
    render(<I18nProvider><TelegramProvider><p>ready</p></TelegramProvider></I18nProvider>);
    await screen.findByText("ready");
    expect(document.documentElement.style.getPropertyValue("--tg-content-safe-top")).toBe("0px");
    expect(document.documentElement.style.getPropertyValue("--tg-effective-content-top")).toBe("8px");
  });

  it("stacks the Telegram content inset under the device safe area and adds no clearance outside the app", () => {
    expect(resolveTelegramContentTop({ contentTop: 46, safeTop: 59, isEmbedded: true })).toEqual({ chromeTop: 105, effectiveTop: 113 });
    expect(resolveTelegramContentTop({ contentTop: 20, safeTop: 24, isEmbedded: true })).toEqual({ chromeTop: 80, effectiveTop: 88 });
    expect(resolveTelegramContentTop({ contentTop: 0, safeTop: 0, isEmbedded: false })).toEqual({ chromeTop: 0, effectiveTop: 0 });
  });

  it("keeps the phone clearance on phones and drops it in desktop and web Telegram", () => {
    expect(resolveTelegramContentTop({ contentTop: 0, safeTop: 0, isEmbedded: true, isMobile: true })).toEqual({ chromeTop: 80, effectiveTop: 88 });
    expect(resolveTelegramContentTop({ contentTop: 0, safeTop: 0, isEmbedded: true, isMobile: false })).toEqual({ chromeTop: 0, effectiveTop: 8 });
    expect(isMobileTelegram("ios")).toBe(true);
    expect(isMobileTelegram("android")).toBe(true);
    expect(isMobileTelegram(undefined)).toBe(true);
    expect(isMobileTelegram("tdesktop")).toBe(false);
    expect(isMobileTelegram("macos")).toBe(false);
    expect(isMobileTelegram("weba")).toBe(false);
  });

  it("registers a modal back handler once when its parent rerenders", async () => {
    const onClick = vi.fn();
    window.Telegram = { WebApp: {
      platform: "ios",
      colorScheme: "light",
      expand: vi.fn(),
      ready: vi.fn(),
      requestFullscreen: vi.fn(),
      disableVerticalSwipes: vi.fn(),
      MainButton: { hide: vi.fn() },
      SettingsButton: { hide: vi.fn() },
      BackButton: { hide: vi.fn(), show: vi.fn(), onClick, offClick: vi.fn() }
    } };

    function DialogExample() {
      useTelegram();
      return <Modal onClose={() => undefined} open title="Dialog">content</Modal>;
    }

    render(<I18nProvider><TelegramProvider><DialogExample /></TelegramProvider></I18nProvider>);
    await screen.findByText("content");
    await new Promise((resolve) => window.setTimeout(resolve, 50));

    expect(onClick).toHaveBeenCalledTimes(1);
  });

  it("keeps the native back button bound while a form handler changes", async () => {
    const onClick = vi.fn();
    window.Telegram = { WebApp: {
      platform: "ios",
      colorScheme: "light",
      expand: vi.fn(),
      ready: vi.fn(),
      requestFullscreen: vi.fn(),
      disableVerticalSwipes: vi.fn(),
      MainButton: { hide: vi.fn() },
      SettingsButton: { hide: vi.fn() },
      BackButton: { hide: vi.fn(), show: vi.fn(), onClick, offClick: vi.fn() }
    } };

    function FormExample() {
      const { setBackButtonHandler } = useTelegram();
      const [value, setValue] = useState(0);
      const goBack = useCallback(() => undefined, [value]);
      useEffect(() => {
        setBackButtonHandler(goBack);
        return () => setBackButtonHandler();
      }, [goBack, setBackButtonHandler]);
      return <button onClick={() => setValue((current) => current + 1)} type="button">update form</button>;
    }

    render(<I18nProvider><TelegramProvider><FormExample /></TelegramProvider></I18nProvider>);
    await screen.findByText("update form");
    expect(onClick).toHaveBeenCalledTimes(1);

    await screen.getByText("update form").click();
    expect(onClick).toHaveBeenCalledTimes(1);
  });
});
