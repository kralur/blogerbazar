import { useEffect, useRef } from "react";
import { useRootScreenVisibility } from "../navigation/RootScreenVisibility";

const screenRefreshEvent = "bloggerbazar:screen-refresh";
type ScreenRefreshDetail = { pending: Promise<unknown>[] };
let screenRefreshVersion = 0;

// Pull-to-refresh and returning to the app ask the visible screens to reload; resolves once they finish.
export async function requestScreenRefresh(): Promise<void> {
  screenRefreshVersion += 1;
  const detail: ScreenRefreshDetail = { pending: [] };
  window.dispatchEvent(new CustomEvent<ScreenRefreshDetail>(screenRefreshEvent, { detail }));
  await Promise.allSettled(detail.pending);
}

// A hidden root screen skips the request and reloads when it is shown again.
export function useScreenRefresh(refresh: () => unknown, enabled = true) {
  const visible = useRootScreenVisibility();
  const refreshRef = useRef(refresh);
  refreshRef.current = refresh;
  const seenVersion = useRef(screenRefreshVersion);

  useEffect(() => {
    if (!enabled || !visible) return;
    if (seenVersion.current !== screenRefreshVersion) {
      seenVersion.current = screenRefreshVersion;
      void Promise.resolve(refreshRef.current()).catch(() => undefined);
    }
    const onRefresh = (event: Event) => {
      seenVersion.current = screenRefreshVersion;
      const result = Promise.resolve(refreshRef.current()).catch(() => undefined);
      (event as CustomEvent<ScreenRefreshDetail>).detail?.pending.push(result);
    };
    window.addEventListener(screenRefreshEvent, onRefresh);
    return () => window.removeEventListener(screenRefreshEvent, onRefresh);
  }, [enabled, visible]);
}
