import { useEffect, useState } from "react";

// The atlas pattern can be switched off (owner, 2026-11-09, package I): the strip on Home, the lists and the wizards
// and the background of the first screens all go, plain lapis stays. On by default; the choice lives on the phone.
const storageKey = "bloggerbazar.atlas";
export const atlasChangedEvent = "bloggerbazar:atlas-changed";

export function isAtlasOn() {
  try {
    return localStorage.getItem(storageKey) !== "off";
  } catch {
    return true;
  }
}

export function setAtlas(on: boolean) {
  try {
    if (on) localStorage.removeItem(storageKey);
    else localStorage.setItem(storageKey, "off");
  } catch {}
  window.dispatchEvent(new Event(atlasChangedEvent));
}

export function useAtlasOn() {
  const [on, setOn] = useState(isAtlasOn);
  useEffect(() => {
    const update = () => setOn(isAtlasOn());
    window.addEventListener(atlasChangedEvent, update);
    return () => window.removeEventListener(atlasChangedEvent, update);
  }, []);
  return on;
}
