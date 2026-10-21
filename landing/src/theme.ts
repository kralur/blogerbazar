import { useState } from "react";

// public/theme-init.js applies the saved choice (or the system one) before the first paint with the same key.
const storageKey = "bloggerbazar.landing.theme";

export function useTheme() {
  const [dark, setDark] = useState(() => document.documentElement.dataset.theme === "dark");
  const toggle = () => {
    const next = !dark;
    document.documentElement.dataset.theme = next ? "dark" : "light";
    try { localStorage.setItem(storageKey, next ? "dark" : "light"); } catch {}
    setDark(next);
  };
  return { dark, toggle };
}
