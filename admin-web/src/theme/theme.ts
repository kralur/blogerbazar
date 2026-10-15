import { useEffect, useState } from "react";

// Light and dark use the same Design System v2 tokens; "system" follows the computer's setting.
// public/theme-init.js applies the saved choice before the first paint with the same storage key.
export type ThemeChoice = "system" | "light" | "dark";
const storageKey = "bloggerbazar.admin.theme";
const darkQuery = "(prefers-color-scheme: dark)";

export function savedTheme(): ThemeChoice {
  try {
    const value = localStorage.getItem(storageKey);
    if (value === "light" || value === "dark") return value;
  } catch {}
  return "system";
}

function systemIsDark() {
  return typeof window.matchMedia === "function" && window.matchMedia(darkQuery).matches;
}

export function applyTheme(choice: ThemeChoice) {
  const dark = choice === "dark" || (choice === "system" && systemIsDark());
  document.documentElement.dataset.theme = dark ? "dark" : "light";
}

export function useTheme() {
  const [choice, setChoiceState] = useState<ThemeChoice>(savedTheme);
  useEffect(() => {
    applyTheme(choice);
    if (choice !== "system" || typeof window.matchMedia !== "function") return;
    const media = window.matchMedia(darkQuery);
    const follow = () => applyTheme("system");
    media.addEventListener?.("change", follow);
    return () => media.removeEventListener?.("change", follow);
  }, [choice]);
  const setChoice = (next: ThemeChoice) => {
    try {
      if (next === "system") localStorage.removeItem(storageKey); else localStorage.setItem(storageKey, next);
    } catch {}
    setChoiceState(next);
  };
  return { choice, setChoice };
}
