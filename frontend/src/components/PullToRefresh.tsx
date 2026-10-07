import { useEffect, useRef, useState } from "react";
import { useI18n } from "../i18n";
import { requestScreenRefresh } from "../hooks/useScreenRefresh";
import { useTelegram } from "../telegram/TelegramProvider";

const triggerDistance = 72;
const maxDistance = 110;
const minimumSpinMs = 450;

// Telegram's own vertical swipe is disabled, so pulling down at the top of a screen reloads its data.
export function PullToRefresh({ enabled }: { enabled: boolean }) {
  const { t } = useI18n();
  const { haptic } = useTelegram();
  const [distance, setDistance] = useState(0);
  const [refreshing, setRefreshing] = useState(false);
  const startY = useRef<number | null>(null);
  const distanceRef = useRef(0);
  const refreshingRef = useRef(false);
  const armedRef = useRef(false);

  useEffect(() => {
    if (!enabled) return;
    const canStart = (target: EventTarget | null) => {
      if (refreshingRef.current || window.scrollY > 0) return false;
      if (document.querySelector("[aria-modal='true']")) return false;
      const element = target instanceof Element ? target : null;
      if (element?.closest("input, textarea, select, [contenteditable='true']")) return false;
      // Inside a scrolled inner list the gesture belongs to that list.
      for (let node = element; node && node !== document.body; node = node.parentElement) {
        if (node.scrollTop > 0) return false;
      }
      return true;
    };
    const onStart = (event: TouchEvent) => {
      startY.current = event.touches.length === 1 && canStart(event.target) ? event.touches[0].clientY : null;
    };
    const onMove = (event: TouchEvent) => {
      if (startY.current === null) return;
      const delta = event.touches[0].clientY - startY.current;
      if (delta <= 0 || window.scrollY > 0) {
        if (distanceRef.current) { distanceRef.current = 0; setDistance(0); }
        return;
      }
      const next = Math.min(maxDistance, delta * 0.5);
      distanceRef.current = next;
      setDistance(next);
      const armed = next >= triggerDistance;
      if (armed && !armedRef.current) haptic.impact();
      armedRef.current = armed;
    };
    const onEnd = () => {
      if (startY.current === null) return;
      startY.current = null;
      const pulled = distanceRef.current;
      distanceRef.current = 0;
      armedRef.current = false;
      if (pulled < triggerDistance) { setDistance(0); return; }
      refreshingRef.current = true;
      setRefreshing(true);
      setDistance(triggerDistance);
      void Promise.all([requestScreenRefresh(), new Promise((resolve) => window.setTimeout(resolve, minimumSpinMs))]).finally(() => {
        refreshingRef.current = false;
        setRefreshing(false);
        setDistance(0);
      });
    };
    document.addEventListener("touchstart", onStart, { passive: true });
    document.addEventListener("touchmove", onMove, { passive: true });
    document.addEventListener("touchend", onEnd);
    document.addEventListener("touchcancel", onEnd);
    return () => {
      document.removeEventListener("touchstart", onStart);
      document.removeEventListener("touchmove", onMove);
      document.removeEventListener("touchend", onEnd);
      document.removeEventListener("touchcancel", onEnd);
    };
  }, [enabled, haptic]);

  if (!distance && !refreshing) return null;
  const progress = Math.min(1, distance / triggerDistance);
  const circumference = 2 * Math.PI * 9;
  return <div aria-label={refreshing ? t("common.refreshing") : undefined} aria-live="polite" className="pull-refresh" role={refreshing ? "status" : undefined} style={{ transform: `translate(-50%, ${distance - 44}px)` }}>
    <svg aria-hidden="true" className={refreshing ? "pull-refresh__ring pull-refresh__ring--spinning" : "pull-refresh__ring"} height="24" viewBox="0 0 24 24" width="24">
      <circle className="pull-refresh__track" cx="12" cy="12" fill="none" r="9" strokeWidth="2.5" />
      <circle className="pull-refresh__progress" cx="12" cy="12" fill="none" r="9" strokeDasharray={circumference} strokeDashoffset={refreshing ? circumference * 0.7 : circumference * (1 - progress)} strokeLinecap="round" strokeWidth="2.5" transform="rotate(-90 12 12)" />
    </svg>
  </div>;
}
