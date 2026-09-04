import { useEffect, useRef } from "react";

const campaignDataChangedEvent = "bloggerbazar:campaign-data-changed";
let campaignDataVersion = 0;

export function notifyCampaignDataChanged() {
  campaignDataVersion += 1;
  window.dispatchEvent(new Event(campaignDataChangedEvent));
}

export function useCampaignDataRefresh(refresh: () => void, enabled = true) {
  const lastSeenVersion = useRef(campaignDataVersion);

  useEffect(() => {
    if (!enabled) return;
    const refreshIfStale = () => {
      lastSeenVersion.current = campaignDataVersion;
      refresh();
    };
    if (lastSeenVersion.current !== campaignDataVersion) refreshIfStale();
    window.addEventListener(campaignDataChangedEvent, refreshIfStale);
    return () => window.removeEventListener(campaignDataChangedEvent, refreshIfStale);
  }, [enabled, refresh]);
}
