const historyOriginKey = "bloggerbazarNavigationOrigin";

type HistoryOriginState = Record<string, unknown> & {
  [historyOriginKey]?: {
    from: string;
    to: string;
  };
};

export function replaceWithHistoryOrigin(from: string, to: string) {
  window.history.replaceState({
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

export function replaceHistoryRoute(to: string) {
  const state = stateWithoutHistoryOrigin(window.history.state);
  window.history.replaceState(state, "", to);
  window.dispatchEvent(new HashChangeEvent("hashchange"));
}

export function stateWithoutHistoryOrigin(state: unknown): Record<string, unknown> {
  if (!state || typeof state !== "object") return {};
  const { [historyOriginKey]: _, ...rest } = state as HistoryOriginState;
  return rest;
}
