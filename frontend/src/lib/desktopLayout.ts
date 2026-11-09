import { useEffect, useState } from "react";

// D64 stage B2: outside Telegram on a wide screen the app is laid out as a website (side menu, wide content).
// Inside Telegram nothing changes. The page reads html[data-desktop="true"].
const wideScreen = "(min-width: 1024px)";

function matches(enabled: boolean) {
  return enabled && typeof window !== "undefined" && typeof window.matchMedia === "function" && window.matchMedia(wideScreen).matches;
}

export function useDesktopLayout(enabled: boolean) {
  const [desktop, setDesktop] = useState(() => matches(enabled));
  useEffect(() => {
    if (!enabled || typeof window.matchMedia !== "function") {
      setDesktop(false);
      return;
    }
    const media = window.matchMedia(wideScreen);
    const update = () => setDesktop(media.matches);
    update();
    media.addEventListener?.("change", update);
    return () => media.removeEventListener?.("change", update);
  }, [enabled]);
  useEffect(() => {
    const root = document.documentElement;
    if (desktop) root.dataset.desktop = "true";
    else delete root.dataset.desktop;
    return () => { delete root.dataset.desktop; };
  }, [desktop]);
  return desktop;
}
