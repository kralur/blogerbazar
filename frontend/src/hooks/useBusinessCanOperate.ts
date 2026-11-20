import { useCallback, useEffect, useState } from "react";
import { getMyBusinessProfile } from "../api/marketplace";
import { useProfileDataRefresh } from "./useProfileDataRefresh";

// D64: a team "viewer" sees the company's campaigns, applications and deals but may not act on them (the server
// answers 403). The screens hide those actions instead of offering buttons that only fail. Anyone else, and a
// company whose role is not known yet, keeps them; the server stays the final check.
let cached: Promise<boolean> | null = null;

function load() {
  cached ??= getMyBusinessProfile().then((profile) => profile.teamRole !== "viewer").catch(() => {
    cached = null;
    return true;
  });
  return cached;
}

export function resetBusinessCanOperate() {
  cached = null;
}

export function useBusinessCanOperate(enabled = true) {
  const [canOperate, setCanOperate] = useState(true);
  const [version, setVersion] = useState(0);
  useProfileDataRefresh(useCallback(() => { resetBusinessCanOperate(); setVersion((value) => value + 1); }, []));
  useEffect(() => {
    if (!enabled) return;
    let active = true;
    void load().then((value) => { if (active) setCanOperate(value); });
    return () => { active = false; };
  }, [enabled, version]);
  return canOperate;
}
