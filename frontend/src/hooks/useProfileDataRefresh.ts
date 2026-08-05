import { useEffect } from "react";
import { useRootScreenVisibility } from "../navigation/RootScreenVisibility";

const profileDataChangedEvent = "bloggerbazar:profile-data-changed";

export function notifyProfileDataChanged() {
  window.dispatchEvent(new Event(profileDataChangedEvent));
}

export function useProfileDataRefresh(refresh: () => void) {
  const rootScreenVisible = useRootScreenVisibility();
  useEffect(() => {
    if (!rootScreenVisible) return;
    window.addEventListener(profileDataChangedEvent, refresh);
    return () => window.removeEventListener(profileDataChangedEvent, refresh);
  }, [refresh, rootScreenVisible]);
}
