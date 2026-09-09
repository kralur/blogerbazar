const historyOriginKey = "bloggerbazarNavigationOrigin";

type HistoryOriginState = Record<string, unknown> & {
  [historyOriginKey]?: {
    from: string;
    to: string;
  };
};

export function navigateWithHistoryOrigin(from: string, to: string) {
  window.history.pushState({
    ...(window.history.state ?? {}),
    [historyOriginKey]: { from, to },
  }, "", to);
  window.dispatchEvent(new HashChangeEvent("hashchange"));
}

export function getHistoryOrigin(state: unknown, destination: string) {
  if (!state || typeof state !== "object") return null;
  const origin = (state as HistoryOriginState)[historyOriginKey];
  return origin?.to === destination ? origin.from : null;
}
