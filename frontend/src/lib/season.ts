import { useEffect, useState } from "react";

// Seasonal artefacts on top of the "Bazar" design (D63): by default the season follows the calendar; the person can
// pin any of the four seasons or the standard design on their phone (owner, 2026-11-09, package I).
export type Season = "winter" | "spring" | "summer" | "autumn";
export type SeasonPreference = "auto" | "off" | Season;
export const seasons: readonly Season[] = ["autumn", "winter", "spring", "summer"];
// Every season has a drawing in `components/SeasonArt.tsx`.
export const drawnSeasons: readonly Season[] = seasons;

const storageKey = "bloggerbazar.season";
const noteKey = "bloggerbazar.seasonNoteDismissed";
export const seasonPreferenceChangedEvent = "bloggerbazar:season-preference-changed";

export function seasonOf(date: Date): Season {
  const month = date.getMonth();
  if (month >= 8 && month <= 10) return "autumn";
  if (month === 11 || month <= 1) return "winter";
  if (month <= 4) return "spring";
  return "summer";
}

export function getSeasonPreference(): SeasonPreference {
  try {
    const value = localStorage.getItem(storageKey);
    if (value === "off") return "off";
    if (value && (seasons as readonly string[]).includes(value)) return value as Season;
    return "auto";
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

// The season the design shows right now, or null for the standard design: the pinned one, else the calendar's.
export function activeSeason(date = new Date()): Season | null {
  const preference = getSeasonPreference();
  if (preference === "off") return null;
  const season = preference === "auto" ? seasonOf(date) : preference;
  return drawnSeasons.includes(season) ? season : null;
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
