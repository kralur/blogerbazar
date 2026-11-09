import { useEffect, useState } from "react";

// Seasonal artefacts on top of the "Bazar" design (D63): the season follows the calendar, the person can switch
// it off on their phone. Only seasons with drawn artefacts show anything; the rest fall back to the plain design.
export type Season = "winter" | "spring" | "summer" | "autumn";
export type SeasonPreference = "auto" | "off";

const storageKey = "bloggerbazar.season";
const noteKey = "bloggerbazar.seasonNoteDismissed";
export const seasonPreferenceChangedEvent = "bloggerbazar:season-preference-changed";
// Seasons with drawings in `components/SeasonArt.tsx`; the others fall back to the plain design.
export const drawnSeasons: readonly Season[] = ["autumn", "winter"];

export function seasonOf(date: Date): Season {
  const month = date.getMonth();
  if (month >= 8 && month <= 10) return "autumn";
  if (month === 11 || month <= 1) return "winter";
  if (month <= 4) return "spring";
  return "summer";
}

export function getSeasonPreference(): SeasonPreference {
  try {
    return localStorage.getItem(storageKey) === "off" ? "off" : "auto";
  } catch {
    return "auto";
  }
}

export function setSeasonPreference(value: SeasonPreference) {
  try {
    if (value === "auto") localStorage.removeItem(storageKey);
    else localStorage.setItem(storageKey, value);
  } catch {}
  applySeason();
  window.dispatchEvent(new Event(seasonPreferenceChangedEvent));
}

// The season the design shows right now, or null for the plain design.
export function activeSeason(date = new Date()): Season | null {
  if (getSeasonPreference() === "off") return null;
  const season = seasonOf(date);
  return drawnSeasons.includes(season) ? season : null;
}

// Whether the current calendar season has artefacts at all (the settings row is shown only then).
export function isSeasonDrawn(date = new Date()) {
  return drawnSeasons.includes(seasonOf(date));
}

// `data-season` on <html> drives the atlas palette in CSS.
export function applySeason() {
  const season = activeSeason();
  if (season) document.documentElement.dataset.season = season;
  else delete document.documentElement.dataset.season;
}

export function installSeason() {
  applySeason();
  window.addEventListener(seasonPreferenceChangedEvent, applySeason);
}

export function useSeason(): Season | null {
  const [season, setSeason] = useState<Season | null>(() => activeSeason());
  useEffect(() => {
    const update = () => setSeason(activeSeason());
    window.addEventListener(seasonPreferenceChangedEvent, update);
    return () => window.removeEventListener(seasonPreferenceChangedEvent, update);
  }, []);
  return season;
}

export function isSeasonNoteDismissed() {
  try {
    return localStorage.getItem(noteKey) === "true";
  } catch {
    return false;
  }
}

export function dismissSeasonNote() {
  try {
    localStorage.setItem(noteKey, "true");
  } catch {}
}
