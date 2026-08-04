import { useEffect, useLayoutEffect } from "react";

export function useScrollRestoration(key: string) {
  const storageKey = `bloggerbazar:scroll:${key}`;

  useLayoutEffect(() => {
    const position = Number(window.sessionStorage.getItem(storageKey) ?? "0");
    const frame = window.requestAnimationFrame(() => window.scrollTo(0, Number.isFinite(position) ? position : 0));
    return () => window.cancelAnimationFrame(frame);
  }, [storageKey]);

  useEffect(() => {
    const savePosition = () => window.sessionStorage.setItem(storageKey, String(window.scrollY));
    window.addEventListener("scroll", savePosition, { passive: true });
    return () => {
      savePosition();
      window.removeEventListener("scroll", savePosition);
    };
  }, [storageKey]);
}
