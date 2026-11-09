import type { ComponentType } from "react";
import { seasonOf, useSeason, type Season } from "../lib/season";
import { AutumnFalling, AutumnStatic } from "./season/AutumnLeaves";
import { WinterFalling, WinterStatic } from "./season/WinterSnow";

// The seasonal artefacts of the "Bazar" design (D63): the screens ask for "static" (a few pieces pinned to the Home
// hero) or "falling" (pieces drifting down a first screen) and this component picks the drawing for the season that
// is on. A new season is added here and in `drawnSeasons`; no screen changes. `force` draws the calendar season
// whatever the preference (the preview in Settings).
type Drawing = { Static: ComponentType; Falling: ComponentType };
const drawings: Partial<Record<Season, Drawing>> = {
  autumn: { Static: AutumnStatic, Falling: AutumnFalling },
  winter: { Static: WinterStatic, Falling: WinterFalling }
};

export function hasSeasonArt(season: Season) {
  return season in drawings;
}

export function SeasonArt({ force = false, variant }: { force?: boolean; variant: "static" | "falling" }) {
  const active = useSeason();
  const season = force ? seasonOf(new Date()) : active;
  const drawing = season ? drawings[season] : undefined;
  if (!drawing) return null;
  return variant === "static" ? <drawing.Static /> : <drawing.Falling />;
}
