import { useState } from "react";
import { useI18n } from "../i18n";
import { dismissSeasonNote, isSeasonNoteDismissed, setSeasonPreference, useSeason } from "../lib/season";

// A quiet line under the Home strip while the seasonal design is on: what it is, and a way to switch it off
// without looking for the setting. "Hide" keeps the design and removes the line for good.
export function SeasonNote() {
  const { t } = useI18n();
  const season = useSeason();
  const [dismissed, setDismissed] = useState(isSeasonNoteDismissed);
  if (!season || dismissed) return null;
  const hide = () => { dismissSeasonNote(); setDismissed(true); };
  return <p className="season-note">
    <span className="season-note__text">{t(`home.season.${season}`)}</span>
    <button className="season-note__action" onClick={() => setSeasonPreference("off")} type="button">{t("home.seasonOff")}</button>
    <button aria-label={t("home.seasonHide")} className="season-note__hide" onClick={hide} type="button">×</button>
  </p>;
}
