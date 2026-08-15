export const TelegramLaunch = {
  splashBackground: "#F8FAFC",
  splashHeader: "#F8FAFC",
  accent: "#2563EB",
  accentHighlight: "#06B6D4"
} as const;

// Some embedded Telegram clients report a zero content inset until fullscreen
// settles. This fallback is used only when the Telegram WebApp bridge exists.
export const TelegramSafeArea = {
  contentTopFallback: 56
} as const;
