import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState, type ReactNode } from "react";
import { BloggerBazarLogo } from "../components/BloggerBazarLogo";
import { TelegramLaunch } from "./telegramTheme";

type TelegramUser = { id: number; username?: string; first_name?: string };
type TelegramBackButton = { show?: () => void; hide?: () => void; onClick?: (handler: () => void) => void; offClick?: (handler: () => void) => void };
type TelegramHaptic = { selectionChanged?: () => void; impactOccurred?: (style: "light" | "medium" | "heavy" | "rigid" | "soft") => void; notificationOccurred?: (type: "error" | "success" | "warning") => void };
type TelegramInset = { top?: number; bottom?: number; left?: number; right?: number };
type TelegramTheme = { bg_color?: string; secondary_bg_color?: string; text_color?: string; hint_color?: string; link_color?: string; button_color?: string; button_text_color?: string; section_separator_color?: string };
type TelegramNativeButton = { hide?: () => void };
type TelegramWebApp = {
  initData?: string;
  initDataUnsafe?: { user?: TelegramUser };
  version?: string;
  colorScheme?: "light" | "dark";
  themeParams?: TelegramTheme;
  viewportHeight?: number;
  viewportStableHeight?: number;
  safeAreaInset?: TelegramInset;
  contentSafeAreaInset?: TelegramInset;
  ready?: () => void;
  expand?: () => void;
  requestFullscreen?: () => void;
  setHeaderColor?: (color: string) => void;
  setBackgroundColor?: (color: string) => void;
  BackButton?: TelegramBackButton;
  MainButton?: TelegramNativeButton;
  SettingsButton?: TelegramNativeButton;
  HapticFeedback?: TelegramHaptic;
  enableVerticalSwipes?: () => void;
  openLink?: (url: string) => void;
  openTelegramLink?: (url: string) => void;
  onEvent?: (event: "themeChanged" | "viewportChanged" | "fullscreenChanged" | "safeAreaChanged" | "contentSafeAreaChanged", handler: () => void) => void;
  offEvent?: (event: "themeChanged" | "viewportChanged" | "fullscreenChanged" | "safeAreaChanged" | "contentSafeAreaChanged", handler: () => void) => void;
};

declare global {
  interface Window { Telegram?: { WebApp?: TelegramWebApp } }
}

function webApp(): TelegramWebApp | undefined {
  return typeof window === "undefined" ? undefined : window.Telegram?.WebApp;
}

export const telegramBridge = {
  get initData() { return webApp()?.initData ?? ""; },
  get user() { return webApp()?.initDataUnsafe?.user; },
  get isTelegram() { return Boolean(webApp()?.initData); }
};

type TelegramContextValue = {
  isTelegram: boolean;
  user?: TelegramUser;
  version?: string;
  theme: TelegramTheme;
  colorScheme: "light" | "dark";
  viewportHeight?: number;
  setBackButtonHandler: (handler?: () => void) => void;
  registerBackButtonHandler: (handler: () => void) => () => void;
  haptic: { selection: () => void; impact: () => void; success: () => void; error: () => void; warning: () => void };
  openLink: (url: string) => void;
};

const TelegramContext = createContext<TelegramContextValue | null>(null);

function SplashScreen() {
  return <main aria-label="BloggerBazar" className="splash-screen"><div className="splash-screen__logo"><BloggerBazarLogo size={88} /></div><h1>BloggerBazar</h1><div className="splash-screen__shimmer" /></main>;
}

export function TelegramProvider({ children }: { children: ReactNode }) {
  const [booting, setBooting] = useState(true);
  const [backHandler, setBackHandler] = useState<(() => void) | undefined>();
  const [overlayBackHandlers, setOverlayBackHandlers] = useState<Array<{ id: number; handler: () => void }>>([]);
  const nextBackHandlerId = useRef(0);
  const app = webApp();
  const [environment, setEnvironment] = useState(() => ({
    colorScheme: app?.colorScheme ?? "light" as const,
    viewportHeight: app?.viewportStableHeight ?? app?.viewportHeight,
    theme: app?.themeParams ?? {}
  }));
  const setBackButtonHandler = useCallback((handler?: () => void) => setBackHandler(() => handler), []);
  const registerBackButtonHandler = useCallback((handler: () => void) => {
    const id = ++nextBackHandlerId.current;
    setOverlayBackHandlers((handlers) => [...handlers, { id, handler }]);
    return () => setOverlayBackHandlers((handlers) => handlers.filter((item) => item.id !== id));
  }, []);

  useEffect(() => {
    app?.ready?.();
    app?.expand?.();
    try {
      app?.requestFullscreen?.();
    } catch {
      app?.expand?.();
    }
    app?.enableVerticalSwipes?.();
    app?.MainButton?.hide?.();
    app?.SettingsButton?.hide?.();

    const applyEnvironment = () => {
      const theme = app?.themeParams;
      const contentInsets = app?.contentSafeAreaInset;
      const safeInsets = app?.safeAreaInset;
      const root = document.documentElement;
      const background = theme?.bg_color ?? TelegramLaunch.splashBackground;
      const secondaryBackground = theme?.secondary_bg_color ?? background;
      root.dataset.telegramTheme = app?.colorScheme ?? "light";
      root.style.setProperty("--telegram-bg", background);
      root.style.setProperty("--telegram-secondary-bg", secondaryBackground);
      root.style.setProperty("--telegram-text", theme?.text_color ?? "#111827");
      root.style.setProperty("--telegram-hint", theme?.hint_color ?? "#64748b");
      root.style.setProperty("--telegram-separator", theme?.section_separator_color ?? "rgba(148, 163, 184, .35)");
      root.style.setProperty("--telegram-button", theme?.button_color ?? TelegramLaunch.accent);
      root.style.setProperty("--tg-safe-area-top", `${safeInsets?.top ?? 0}px`);
      root.style.setProperty("--tg-safe-area-bottom", `${safeInsets?.bottom ?? 0}px`);
      root.style.setProperty("--tg-content-safe-top", `${contentInsets?.top ?? safeInsets?.top ?? 0}px`);
      root.style.setProperty("--tg-content-safe-bottom", `${contentInsets?.bottom ?? safeInsets?.bottom ?? 0}px`);
      root.style.setProperty("--tg-viewport-height", `${app?.viewportStableHeight ?? app?.viewportHeight ?? window.innerHeight}px`);
      try {
        app?.setHeaderColor?.(theme?.bg_color ?? TelegramLaunch.splashHeader);
        app?.setBackgroundColor?.(background);
      } catch {}
      setEnvironment({
        colorScheme: app?.colorScheme ?? "light",
        viewportHeight: app?.viewportStableHeight ?? app?.viewportHeight,
        theme: theme ?? {}
      });
    };

    applyEnvironment();
    const events = ["themeChanged", "viewportChanged", "fullscreenChanged", "safeAreaChanged", "contentSafeAreaChanged"] as const;
    events.forEach((event) => app?.onEvent?.(event, applyEnvironment));
    const timer = window.setTimeout(() => setBooting(false), 260);
    return () => {
      window.clearTimeout(timer);
      events.forEach((event) => app?.offEvent?.(event, applyEnvironment));
    };
  }, [app]);

  useEffect(() => {
    const button = app?.BackButton;
    if (!button) return;
    const activeHandler = overlayBackHandlers[overlayBackHandlers.length - 1]?.handler ?? backHandler;
    if (!activeHandler) {
      button.hide?.();
      return;
    }
    button.show?.();
    button.onClick?.(activeHandler);
    return () => {
      button.offClick?.(activeHandler);
      button.hide?.();
    };
  }, [app, backHandler, overlayBackHandlers]);

  const value = useMemo<TelegramContextValue>(() => ({
    isTelegram: Boolean(app?.initData),
    user: app?.initDataUnsafe?.user,
    version: app?.version,
    theme: environment.theme,
    colorScheme: environment.colorScheme,
    viewportHeight: environment.viewportHeight,
    setBackButtonHandler,
    registerBackButtonHandler,
    haptic: {
      selection: () => app?.HapticFeedback?.selectionChanged?.(),
      impact: () => app?.HapticFeedback?.impactOccurred?.("light"),
      success: () => app?.HapticFeedback?.notificationOccurred?.("success"),
      error: () => app?.HapticFeedback?.notificationOccurred?.("error"),
      warning: () => app?.HapticFeedback?.notificationOccurred?.("warning")
    },
    openLink: (url) => {
      if (url.startsWith("https://t.me/")) {
        app?.openTelegramLink?.(url);
      } else if (app?.openLink) {
        app.openLink(url);
      } else {
        window.location.assign(url);
      }
    }
  }), [app, environment, registerBackButtonHandler, setBackButtonHandler]);

  return <TelegramContext.Provider value={value}>{booting ? <SplashScreen /> : children}</TelegramContext.Provider>;
}

export function useTelegram() {
  const context = useContext(TelegramContext);
  if (!context) throw new Error("useTelegram must be used inside TelegramProvider.");
  return context;
}
