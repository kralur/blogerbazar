export type ThemePreference = "telegram" | "light" | "dark";
export type ColorScheme = "light" | "dark";

const storageKey = "bloggerbazar.theme";
export const themePreferenceChangedEvent = "bloggerbazar:theme-preference-changed";

// Per-device choice; "telegram" (default) follows the Telegram client theme.
export function getThemePreference(): ThemePreference {
  try {
    const value = localStorage.getItem(storageKey);
    return value === "light" || value === "dark" ? value : "telegram";
  } catch {
    return "telegram";
  }
}

export function setThemePreference(value: ThemePreference) {
  try {
    if (value === "telegram") localStorage.removeItem(storageKey);
    else localStorage.setItem(storageKey, value);
  } catch {}
  window.dispatchEvent(new Event(themePreferenceChangedEvent));
}

export function resolveColorScheme(preference: ThemePreference, telegramScheme?: ColorScheme): ColorScheme {
  return preference === "telegram" ? telegramScheme ?? "light" : preference;
}
