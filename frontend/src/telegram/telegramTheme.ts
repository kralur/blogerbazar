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
  contentGap: 16
} as const;

export function resolveTelegramContentTop({
  contentTop = 0,
  safeTop = 0,
  isEmbedded = false
}: {
  contentTop?: number;
  safeTop?: number;
  isEmbedded?: boolean;
}) {
  const reportedTop = Math.max(contentTop, safeTop, 0);
  const chromeTop = isEmbedded
    ? Math.max(reportedTop, TelegramSafeArea.minimumChromeTop)
    : reportedTop;

  return {
    chromeTop,
    effectiveTop: chromeTop + (isEmbedded ? TelegramSafeArea.contentGap : 0)
  };
}
