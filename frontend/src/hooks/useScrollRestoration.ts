import { useEffect, useLayoutEffect } from "react";
import { useRootScreenVisibility } from "../navigation/RootScreenVisibility";

export function useScrollRestoration(key: string, enabled = true) {
  const storageKey = `bloggerbazar:scroll:${key}`;
  const rootScreenVisible = useRootScreenVisibility();
  const active = enabled && rootScreenVisible;

  useLayoutEffect(() => {
    if (!active) return;
    const position = Number(window.sessionStorage.getItem(storageKey) ?? "0");
    const frame = window.requestAnimationFrame(() => window.scrollTo(0, Number.isFinite(position) ? position : 0));
    return () => window.cancelAnimationFrame(frame);
  }, [active, storageKey]);

  useEffect(() => {
    if (!active) return;
    const savePosition = () => window.sessionStorage.setItem(storageKey, String(window.scrollY));
    window.addEventListener("scroll", savePosition, { passive: true });
    return () => {
      savePosition();
      window.removeEventListener("scroll", savePosition);
    };
  }, [active, storageKey]);
}
