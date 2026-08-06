import { useEffect, useRef } from "react";
import { useRootScreenVisibility } from "../navigation/RootScreenVisibility";

const profileDataChangedEvent = "bloggerbazar:profile-data-changed";
let profileDataVersion = 0;

export function notifyProfileDataChanged() {
  profileDataVersion += 1;
  window.dispatchEvent(new Event(profileDataChangedEvent));
}

export function useProfileDataRefresh(refresh: () => void) {
  const rootScreenVisible = useRootScreenVisibility();
  const lastSeenVersion = useRef(profileDataVersion);
  useEffect(() => {
    if (!rootScreenVisible) return;
    const refreshIfStale = () => {
      lastSeenVersion.current = profileDataVersion;
      refresh();
    };
    if (lastSeenVersion.current !== profileDataVersion) refreshIfStale();
    window.addEventListener(profileDataChangedEvent, refreshIfStale);
    return () => window.removeEventListener(profileDataChangedEvent, refreshIfStale);
  }, [refresh, rootScreenVisible]);
}
