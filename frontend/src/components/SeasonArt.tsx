import type { ComponentType } from "react";
import { seasonOf, useSeason, type Season } from "../lib/season";
import { AutumnFalling, AutumnStatic } from "./season/AutumnLeaves";
import { SpringFalling, SpringStatic } from "./season/SpringBlossom";
import { SummerFalling, SummerStatic } from "./season/SummerFireflies";
import { WinterFalling, WinterStatic } from "./season/WinterSnow";

// The seasonal artefacts of the "Bazar" design (D63): the screens ask for "static" (a few pieces pinned to the Home
// hero) or "falling" (pieces drifting over a first screen) and this component picks the drawing for the season that
// is on. A new drawing is added here and in `drawnSeasons`; no screen changes. `season` draws that season whatever
// the preference (the previews in Settings); `force` does the same for the calendar season.
type Drawing = { Static: ComponentType; Falling: ComponentType };
const drawings: Partial<Record<Season, Drawing>> = {
  autumn: { Static: AutumnStatic, Falling: AutumnFalling },
  winter: { Static: WinterStatic, Falling: WinterFalling },
  spring: { Static: SpringStatic, Falling: SpringFalling },
  summer: { Static: SummerStatic, Falling: SummerFalling }
};

export function hasSeasonArt(season: Season) {
  return season in drawings;
}

export function SeasonArt({ force = false, season: wanted, variant }: { force?: boolean; season?: Season; variant: "static" | "falling" }) {
  const active = useSeason();
  const season = wanted ?? (force ? seasonOf(new Date()) : active);
  const drawing = season ? drawings[season] : undefined;
  if (!drawing) return null;
  return variant === "static" ? <drawing.Static /> : <drawing.Falling />;
}
