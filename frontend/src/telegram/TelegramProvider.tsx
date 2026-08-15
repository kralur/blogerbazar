import { createContext, useCallback, useContext, useEffect, useLayoutEffect, useMemo, useRef, useState, type ReactNode } from "react";
import { LaunchScreen } from "../components/LaunchScreen";
import { resolveTelegramContentTop, TelegramLaunch } from "./telegramTheme";

type TelegramUser = { id: number; username?: string; first_name?: string; photo_url?: string; language_code?: string };
type TelegramBackButton = { show?: () => void; hide?: () => void; onClick?: (handler: () => void) => void; offClick?: (handler: () => void) => void };
type TelegramHaptic = { selectionChanged?: () => void; impactOccurred?: (style: "light" | "medium" | "heavy" | "rigid" | "soft") => void; notificationOccurred?: (type: "error" | "success" | "warning") => void };
type TelegramInset = { top?: number; bottom?: number; left?: number; right?: number };
type TelegramTheme = { bg_color?: string; secondary_bg_color?: string; text_color?: string; hint_color?: string; link_color?: string; button_color?: string; button_text_color?: string; section_separator_color?: string };
type TelegramNativeButton = { hide?: () => void };
type TelegramClosingBehavior = { enableConfirmation?: () => void; disableConfirmation?: () => void };
type TelegramWebApp = {
  initData?: string;
  initDataUnsafe?: { user?: TelegramUser };
  platform?: string;
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
  setBottomBarColor?: (color: string) => void;
  BackButton?: TelegramBackButton;
  MainButton?: TelegramNativeButton;
  SettingsButton?: TelegramNativeButton;
  ClosingBehavior?: TelegramClosingBehavior;
  HapticFeedback?: TelegramHaptic;
  disableVerticalSwipes?: () => void;
  openLink?: (url: string) => void;
  openTelegramLink?: (url: string) => void;
  onEvent?: (event: "themeChanged" | "viewportChanged" | "fullscreenChanged" | "fullscreenFailed" | "orientationChanged" | "safeAreaChanged" | "contentSafeAreaChanged", handler: () => void) => void;
  offEvent?: (event: "themeChanged" | "viewportChanged" | "fullscreenChanged" | "fullscreenFailed" | "orientationChanged" | "safeAreaChanged" | "contentSafeAreaChanged", handler: () => void) => void;
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
  setClosingConfirmation: (enabled: boolean) => void;
  haptic: { selection: () => void; impact: () => void; success: () => void; error: () => void; warning: () => void };
  openLink: (url: string) => void;
};

const TelegramContext = createContext<TelegramContextValue | null>(null);

export function TelegramProvider({ children }: { children: ReactNode }) {
  const [booting, setBooting] = useState(true);
  const backHandlerRef = useRef<(() => void) | undefined>();
  const [hasBackHandler, setHasBackHandler] = useState(false);
  const [overlayBackHandlers, setOverlayBackHandlers] = useState<Array<{ id: number; handler: () => void }>>([]);
  const overlayBackHandlersRef = useRef<Array<{ id: number; handler: () => void }>>([]);
  const nextBackHandlerId = useRef(0);
  const app = webApp();
  const [environment, setEnvironment] = useState(() => ({
    colorScheme: app?.colorScheme ?? "light" as const,
    viewportHeight: app?.viewportStableHeight ?? app?.viewportHeight,
    theme: app?.themeParams ?? {}
  }));
  const setBackButtonHandler = useCallback((handler?: () => void) => {
    backHandlerRef.current = handler;
    setHasBackHandler((current) => current === Boolean(handler) ? current : Boolean(handler));
  }, []);
  const setClosingConfirmation = useCallback((enabled: boolean) => {
    if (enabled) app?.ClosingBehavior?.enableConfirmation?.();
    else app?.ClosingBehavior?.disableConfirmation?.();
  }, [app]);
  const registerBackButtonHandler = useCallback((handler: () => void) => {
    const id = ++nextBackHandlerId.current;
    setOverlayBackHandlers((handlers) => {
      const nextHandlers = [...handlers, { id, handler }];
      overlayBackHandlersRef.current = nextHandlers;
      return nextHandlers;
    });
    return () => setOverlayBackHandlers((handlers) => {
      const nextHandlers = handlers.filter((item) => item.id !== id);
      overlayBackHandlersRef.current = nextHandlers;
      return nextHandlers;
    });
  }, []);

  useLayoutEffect(() => {
    const applyEnvironment = () => {
      const theme = app?.themeParams;
      const contentInsets = app?.contentSafeAreaInset;
      const safeInsets = app?.safeAreaInset;
      const root = document.documentElement;
      const { chromeTop, effectiveTop } = resolveTelegramContentTop({
        contentTop: contentInsets?.top,
        safeTop: safeInsets?.top,
        isEmbedded: Boolean(app)
      });
      const isDark = app?.colorScheme === "dark";
      const background = isDark ? TelegramLaunch.splashBackgroundDark : TelegramLaunch.splashBackground;
      const secondaryBackground = theme?.secondary_bg_color ?? background;
      root.dataset.telegramTheme = app?.colorScheme ?? "light";
      root.dataset.telegramEmbedded = app ? "true" : "false";
      root.style.setProperty("--telegram-bg", background);
      root.style.setProperty("--telegram-secondary-bg", secondaryBackground);
      root.style.setProperty("--telegram-text", theme?.text_color ?? (isDark ? "#F5F5F0" : "#0A0A0A"));
      root.style.setProperty("--telegram-hint", theme?.hint_color ?? (isDark ? "#9A9A92" : "#6F6F68"));
      root.style.setProperty("--telegram-separator", theme?.section_separator_color ?? (isDark ? "#2A2A28" : "#D8D8D0"));
      root.style.setProperty("--telegram-button", theme?.button_color ?? TelegramLaunch.accent);
      root.style.setProperty("--tg-safe-area-top", `${safeInsets?.top ?? 0}px`);
      root.style.setProperty("--tg-safe-area-bottom", `${safeInsets?.bottom ?? 0}px`);
      root.style.setProperty("--tg-content-safe-top", `${chromeTop}px`);
      root.style.setProperty("--tg-effective-content-top", `${effectiveTop}px`);
      root.style.setProperty("--tg-content-safe-bottom", `${Math.max(contentInsets?.bottom ?? 0, safeInsets?.bottom ?? 0)}px`);
      const browserViewportHeight = window.visualViewport?.height ?? window.innerHeight;
      root.style.setProperty("--tg-viewport-height", `${app?.viewportStableHeight ?? app?.viewportHeight ?? browserViewportHeight}px`);
      try {
        const chromeColor = isDark ? TelegramLaunch.splashHeaderDark : TelegramLaunch.splashHeader;
        app?.setHeaderColor?.(chromeColor);
        app?.setBackgroundColor?.(background);
        app?.setBottomBarColor?.(background);
      } catch {}
      setEnvironment({
        colorScheme: app?.colorScheme ?? "light",
        viewportHeight: app?.viewportStableHeight ?? app?.viewportHeight,
        theme: theme ?? {}
      });
    };

    const events = ["themeChanged", "viewportChanged", "fullscreenChanged", "fullscreenFailed", "orientationChanged", "safeAreaChanged", "contentSafeAreaChanged"] as const;
    events.forEach((event) => app?.onEvent?.(event, applyEnvironment));
    const onFullscreenFailed = () => {
      app?.expand?.();
      applyEnvironment();
    };
    const onBrowserViewportChange = () => applyEnvironment();
    app?.onEvent?.("fullscreenFailed", onFullscreenFailed);
    app?.ready?.();
    applyEnvironment();
    app?.expand?.();
    if (app?.platform === "ios" || app?.platform === "android") {
      try {
        app.requestFullscreen?.();
      } catch {
        app.expand?.();
      }
    }
    window.requestAnimationFrame(applyEnvironment);
    const settleTimer = window.setTimeout(applyEnvironment, 250);
    app?.disableVerticalSwipes?.();
    app?.MainButton?.hide?.();
    app?.SettingsButton?.hide?.();
    window.addEventListener("resize", onBrowserViewportChange, { passive: true });
    window.addEventListener("orientationchange", onBrowserViewportChange, { passive: true });
    document.addEventListener("visibilitychange", onBrowserViewportChange);
    setBooting(false);
    return () => {
      window.clearTimeout(settleTimer);
      events.forEach((event) => app?.offEvent?.(event, applyEnvironment));
      app?.offEvent?.("fullscreenFailed", onFullscreenFailed);
      window.removeEventListener("resize", onBrowserViewportChange);
      window.removeEventListener("orientationchange", onBrowserViewportChange);
      document.removeEventListener("visibilitychange", onBrowserViewportChange);
    };
  }, [app]);

  useEffect(() => {
    const preventGestureZoom = (event: Event) => event.preventDefault();
    const preventDoubleTapZoom = (() => {
      let lastTouchEnd = 0;
      return (event: TouchEvent) => {
        const now = Date.now();
        if (now - lastTouchEnd < 300) event.preventDefault();
        lastTouchEnd = now;
      };
    })();

    document.addEventListener("gesturestart", preventGestureZoom, { passive: false });
    document.addEventListener("gesturechange", preventGestureZoom, { passive: false });
    document.addEventListener("gestureend", preventGestureZoom, { passive: false });
    document.addEventListener("touchend", preventDoubleTapZoom, { passive: false });
    return () => {
      document.removeEventListener("gesturestart", preventGestureZoom);
      document.removeEventListener("gesturechange", preventGestureZoom);
      document.removeEventListener("gestureend", preventGestureZoom);
      document.removeEventListener("touchend", preventDoubleTapZoom);
    };
  }, []);

  useEffect(() => {
    const button = app?.BackButton;
    if (!button) return;
    const onBackButtonClick = () => {
      const handlers = overlayBackHandlersRef.current;
      const overlayHandler = handlers.length ? handlers[handlers.length - 1].handler : undefined;
      (overlayHandler ?? backHandlerRef.current)?.();
    };
    button.onClick?.(onBackButtonClick);
    return () => {
      button.offClick?.(onBackButtonClick);
    };
  }, [app]);

  useEffect(() => {
    const button = app?.BackButton;
    if (!button) return;
    if (!hasBackHandler && !overlayBackHandlers.length) {
      button.hide?.();
      return;
    }
    button.show?.();
  }, [app, hasBackHandler, overlayBackHandlers.length]);

  const value = useMemo<TelegramContextValue>(() => ({
    isTelegram: Boolean(app?.initData),
    user: app?.initDataUnsafe?.user,
    version: app?.version,
    theme: environment.theme,
    colorScheme: environment.colorScheme,
    viewportHeight: environment.viewportHeight,
    setBackButtonHandler,
    registerBackButtonHandler,
    setClosingConfirmation,
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
  }), [app, environment, registerBackButtonHandler, setBackButtonHandler, setClosingConfirmation]);

  return <TelegramContext.Provider value={value}>{booting ? <LaunchScreen /> : children}</TelegramContext.Provider>;
}

export function useTelegram() {
  const context = useContext(TelegramContext);
  if (!context) throw new Error("useTelegram must be used inside TelegramProvider.");
  return context;
}
