export const TelegramLaunch = {
  splashBackground: "#F6F6F2",
  splashBackgroundDark: "#090909",
  splashHeader: "#F6F6F2",
  splashHeaderDark: "#090909",
  accent: "#C8FF00",
  accentHighlight: "#B5E600"
} as const;

export const TelegramSafeArea = {
  minimumChromeTop: 80,
  contentGap: 8
} as const;

export function resolveTelegramContentTop({
  contentTop = 0,
  safeTop = 0,
  isEmbedded = false,
  isMobile = true
}: {
  contentTop?: number;
  safeTop?: number;
  isEmbedded?: boolean;
  isMobile?: boolean;
}) {
  // Telegram reports contentSafeAreaInset inside the device safe area (below the status bar),
  // so the Close/menu buttons end at safeTop + contentTop, not at the larger of the two.
  const reportedTop = Math.max(safeTop, 0) + Math.max(contentTop, 0);
  // Only phones draw Telegram's Close/menu buttons over our page; desktop and web keep them in their own window bar.
  const chromeTop = isEmbedded && isMobile
    ? Math.max(reportedTop, TelegramSafeArea.minimumChromeTop)
    : reportedTop;

  return {
    chromeTop,
    effectiveTop: chromeTop + (isEmbedded ? TelegramSafeArea.contentGap : 0)
  };
}
