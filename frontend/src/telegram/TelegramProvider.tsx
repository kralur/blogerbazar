import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from "react";

type TelegramUser = { id: number; username?: string; first_name?: string };
type TelegramBackButton = { show?: () => void; hide?: () => void; onClick?: (handler: () => void) => void; offClick?: (handler: () => void) => void };
type TelegramHaptic = { selectionChanged?: () => void; impactOccurred?: (style: "light" | "medium" | "heavy" | "rigid" | "soft") => void; notificationOccurred?: (type: "error" | "success" | "warning") => void };
type TelegramTheme = { bg_color?: string; secondary_bg_color?: string; text_color?: string; hint_color?: string; button_color?: string; button_text_color?: string };
type TelegramNativeButton = { hide?: () => void };
type TelegramWebApp = {
  initData?: string;
  initDataUnsafe?: { user?: TelegramUser };
  version?: string;
  colorScheme?: "light" | "dark";
  themeParams?: TelegramTheme;
  viewportHeight?: number;
  viewportStableHeight?: number;
  ready?: () => void;
  expand?: () => void;
  openInvoice?: (url: string, callback?: (status: string) => void) => void;
  BackButton?: TelegramBackButton;
  MainButton?: TelegramNativeButton;
  SettingsButton?: TelegramNativeButton;
  HapticFeedback?: TelegramHaptic;
  onEvent?: (event: "themeChanged" | "viewportChanged", handler: () => void) => void;
  offEvent?: (event: "themeChanged" | "viewportChanged", handler: () => void) => void;
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
  get isTelegram() { return Boolean(webApp()?.initData); },
  openInvoice(invoiceLink: string) {
    const app = webApp();
    if (!app?.openInvoice) throw new Error("telegram_invoice_unavailable");
    return new Promise<string>((resolve) => app.openInvoice?.(invoiceLink, resolve));
  }
};

type TelegramContextValue = {
  isTelegram: boolean;
  user?: TelegramUser;
  version?: string;
  theme: TelegramTheme;
  colorScheme: "light" | "dark";
  viewportHeight?: number;
  setBackButtonHandler: (handler?: () => void) => void;
  haptic: { selection: () => void; impact: () => void; success: () => void };
};

const TelegramContext = createContext<TelegramContextValue | null>(null);

function SplashScreen() {
  return <main aria-label="BloggerBazar" className="splash-screen"><div className="splash-screen__orb"><span className="splash-screen__mark">B</span></div><h1>BloggerBazar</h1><div className="splash-screen__shimmer" /></main>;
}

export function TelegramProvider({ children }: { children: ReactNode }) {
  const [booting, setBooting] = useState(true);
  const [backHandler, setBackHandler] = useState<(() => void) | undefined>();
  const app = webApp();
  const [environment, setEnvironment] = useState(() => ({
    colorScheme: app?.colorScheme ?? "light" as const,
    viewportHeight: app?.viewportStableHeight ?? app?.viewportHeight,
    theme: app?.themeParams ?? {}
  }));
  const setBackButtonHandler = useCallback((handler?: () => void) => setBackHandler(() => handler), []);

  useEffect(() => {
    app?.ready?.();
    app?.expand?.();
    app?.MainButton?.hide?.();
    app?.SettingsButton?.hide?.();

    const applyTheme = () => {
      const theme = app?.themeParams;
      const root = document.documentElement;
      root.style.setProperty("--telegram-bg", theme?.bg_color ?? "#f8fafc");
      root.style.setProperty("--telegram-text", theme?.text_color ?? "#111827");
      root.style.setProperty("--telegram-hint", theme?.hint_color ?? "#64748b");
      root.style.setProperty("--telegram-button", theme?.button_color ?? "#2563eb");
      setEnvironment({
        colorScheme: app?.colorScheme ?? "light",
        viewportHeight: app?.viewportStableHeight ?? app?.viewportHeight,
        theme: theme ?? {}
      });
    };

    applyTheme();
    app?.onEvent?.("themeChanged", applyTheme);
    app?.onEvent?.("viewportChanged", applyTheme);
    const timer = window.setTimeout(() => setBooting(false), 600);
    return () => {
      window.clearTimeout(timer);
      app?.offEvent?.("themeChanged", applyTheme);
      app?.offEvent?.("viewportChanged", applyTheme);
    };
  }, [app]);

  useEffect(() => {
    const button = app?.BackButton;
    if (!button) return;
    if (!backHandler) {
      button.hide?.();
      return;
    }
    button.show?.();
    button.onClick?.(backHandler);
    return () => {
      button.offClick?.(backHandler);
      button.hide?.();
    };
  }, [app, backHandler]);

  const value = useMemo<TelegramContextValue>(() => ({
    isTelegram: Boolean(app?.initData),
    user: app?.initDataUnsafe?.user,
    version: app?.version,
    theme: environment.theme,
    colorScheme: environment.colorScheme,
    viewportHeight: environment.viewportHeight,
    setBackButtonHandler,
    haptic: {
      selection: () => app?.HapticFeedback?.selectionChanged?.(),
      impact: () => app?.HapticFeedback?.impactOccurred?.("light"),
      success: () => app?.HapticFeedback?.notificationOccurred?.("success")
    }
  }), [app, environment, setBackButtonHandler]);

  return <TelegramContext.Provider value={value}>{booting ? <SplashScreen /> : children}</TelegramContext.Provider>;
}

export function useTelegram() {
  const context = useContext(TelegramContext);
  if (!context) throw new Error("useTelegram must be used inside TelegramProvider.");
  return context;
}
