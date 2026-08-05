import { useEffect } from "react";

const profileDataChangedEvent = "bloggerbazar:profile-data-changed";

export function notifyProfileDataChanged() {
  window.dispatchEvent(new Event(profileDataChangedEvent));
}

export function useProfileDataRefresh(refresh: () => void) {
  useEffect(() => {
    window.addEventListener(profileDataChangedEvent, refresh);
    return () => window.removeEventListener(profileDataChangedEvent, refresh);
  }, [refresh]);
}
