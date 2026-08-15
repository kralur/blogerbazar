export const TelegramLaunch = {
  splashBackground: "#F8FAFC",
  splashHeader: "#F8FAFC",
  accent: "#2563EB",
  accentHighlight: "#06B6D4"
} as const;

export const TelegramSafeArea = {
  chromeTopFallback: 80,
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
  const chromeTop = reportedTop || (isEmbedded ? TelegramSafeArea.chromeTopFallback : 0);

  return {
    chromeTop,
    effectiveTop: chromeTop + (isEmbedded ? TelegramSafeArea.contentGap : 0)
  };
}
