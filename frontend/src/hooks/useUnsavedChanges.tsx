import { useCallback, useEffect, useRef, useState } from "react";
import { Button, Modal } from "../components/ui";
import { useI18n } from "../i18n";
import { guardedNavigationEvent, type GuardedNavigationDetail } from "../navigation/guardedNavigation";
import { useTelegram } from "../telegram/TelegramProvider";

export type UnsavedChangesGuard = {
  cancelLeave: () => void;
  confirmLeave: () => void;
  pendingHash: string | null;
  hasPendingLeave: boolean;
  markClean: () => void;
  requestLeave: (action: () => void) => void;
};

const historyGuardKey = "bloggerbazarUnsavedGuard";
type HistoryState = Record<string, unknown> & { [historyGuardKey]?: { hash: string } };

function hasHistoryGuard(state: unknown, hash: string) {
  return Boolean(state && typeof state === "object" && (state as HistoryState)[historyGuardKey]?.hash === hash);
}

function withoutHistoryGuard(state: unknown): HistoryState {
  if (!state || typeof state !== "object") return {};
  const { [historyGuardKey]: _, ...rest } = state as HistoryState;
  return rest;
}

export function useUnsavedChanges(isDirty: boolean): UnsavedChangesGuard {
  const { setClosingConfirmation } = useTelegram();
  const currentHash = useRef(window.location.hash);
  const bypassGuard = useRef(false);
  const leavingRef = useRef(false);
  const dirtyRef = useRef(isDirty);
  dirtyRef.current = leavingRef.current ? false : isDirty;
  const pendingLeaveRef = useRef(false);
  const sentinelActiveRef = useRef(false);
  const pendingHistoryBackRef = useRef(false);
  const restoringSentinelRef = useRef(false);
  const continuingCleanBackRef = useRef(false);
  const [pendingHash, setPendingHash] = useState<string | null>(null);
  const pendingAction = useRef<(() => void) | null>(null);

  const deactivateHistoryGuard = useCallback(() => {
    if (hasHistoryGuard(window.history.state, currentHash.current)) {
      window.history.replaceState(withoutHistoryGuard(window.history.state), "", window.location.href);
    }
    sentinelActiveRef.current = false;
  }, []);

  const requestLeave = useCallback((action: () => void) => {
    if (!dirtyRef.current) {
      action();
      return;
    }
    if (pendingLeaveRef.current) return;
    pendingLeaveRef.current = true;
    pendingAction.current = action;
    setPendingHash("");
  }, []);

  useEffect(() => {
    if (!isDirty || sentinelActiveRef.current) return;
    if (!hasHistoryGuard(window.history.state, currentHash.current)) {
      window.history.pushState({
        ...(window.history.state ?? {}),
        [historyGuardKey]: { hash: currentHash.current },
      }, "", window.location.href);
    }
    sentinelActiveRef.current = true;
  }, [isDirty]);

  useEffect(() => {
    const onHashChange = () => {
      const nextHash = window.location.hash;
      if (leavingRef.current) {
        currentHash.current = nextHash;
        return;
      }
      if (bypassGuard.current) {
        bypassGuard.current = false;
        currentHash.current = nextHash;
        return;
      }
      if (dirtyRef.current && nextHash !== currentHash.current) {
        setPendingHash(nextHash);
        bypassGuard.current = true;
        window.location.hash = currentHash.current;
        return;
      }
      currentHash.current = nextHash;
    };
    const onPopState = (event: PopStateEvent) => {
      const nextHash = window.location.hash;
      if (leavingRef.current) {
        currentHash.current = nextHash;
        return;
      }
      if (bypassGuard.current) {
        bypassGuard.current = false;
        currentHash.current = nextHash;
        return;
      }
      if (restoringSentinelRef.current && hasHistoryGuard(event.state, nextHash)) {
        restoringSentinelRef.current = false;
        currentHash.current = nextHash;
        return;
      }
      if (continuingCleanBackRef.current) {
        continuingCleanBackRef.current = false;
        currentHash.current = nextHash;
        return;
      }
      if (!sentinelActiveRef.current || nextHash !== currentHash.current || hasHistoryGuard(event.state, nextHash)) return;
      if (!dirtyRef.current) {
        continuingCleanBackRef.current = true;
        window.history.back();
        return;
      }
      if (pendingLeaveRef.current) return;
      pendingHistoryBackRef.current = true;
      requestLeave(() => {
        bypassGuard.current = true;
        window.history.back();
      });
    };
    const onBeforeUnload = (event: BeforeUnloadEvent) => {
      if (!dirtyRef.current) return;
      event.preventDefault();
      event.returnValue = "";
    };
    const onDocumentClick = (event: MouseEvent) => {
      if (!dirtyRef.current || event.defaultPrevented || event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;
      const target = event.target;
      if (!(target instanceof Element)) return;
      const href = target.closest<HTMLAnchorElement>('a[href^="#/"]')?.getAttribute("href");
      if (!href || href === currentHash.current) return;
      event.preventDefault();
      if (!pendingLeaveRef.current) {
        requestLeave(() => {
          window.location.hash = href.slice(1);
        });
      }
    };
    const onGuardedNavigation = (event: Event) => {
      const destination = (event as CustomEvent<GuardedNavigationDetail>).detail.destination;
      if (!dirtyRef.current || destination === currentHash.current) return;
      event.preventDefault();
      if (!pendingLeaveRef.current) {
        requestLeave(() => {
          window.location.hash = destination;
        });
      }
    };
    window.addEventListener("hashchange", onHashChange);
    window.addEventListener("popstate", onPopState);
    window.addEventListener("beforeunload", onBeforeUnload);
    window.addEventListener(guardedNavigationEvent, onGuardedNavigation);
    document.addEventListener("click", onDocumentClick, true);
    return () => {
      window.removeEventListener("hashchange", onHashChange);
      window.removeEventListener("popstate", onPopState);
      window.removeEventListener("beforeunload", onBeforeUnload);
      window.removeEventListener(guardedNavigationEvent, onGuardedNavigation);
      document.removeEventListener("click", onDocumentClick, true);
    };
  }, [requestLeave]);

  useEffect(() => {
    setClosingConfirmation(isDirty);
    return () => setClosingConfirmation(false);
  }, [isDirty, setClosingConfirmation]);

  const cancelLeave = useCallback(() => {
    pendingAction.current = null;
    pendingLeaveRef.current = false;
    if (pendingHistoryBackRef.current) {
      pendingHistoryBackRef.current = false;
      restoringSentinelRef.current = true;
      window.history.forward();
    }
    setPendingHash(null);
  }, []);
  const confirmLeave = useCallback(() => {
    const action = pendingAction.current;
    if (action) {
      bypassGuard.current = true;
      leavingRef.current = true;
      dirtyRef.current = false;
      deactivateHistoryGuard();
      action();
    } else if (pendingHash) {
      bypassGuard.current = true;
      leavingRef.current = true;
      dirtyRef.current = false;
      deactivateHistoryGuard();
      window.location.hash = pendingHash;
    }
    pendingAction.current = null;
    pendingLeaveRef.current = false;
    pendingHistoryBackRef.current = false;
    setPendingHash(null);
  }, [deactivateHistoryGuard, pendingHash]);

  const markClean = useCallback(() => {
    leavingRef.current = true;
    dirtyRef.current = false;
    deactivateHistoryGuard();
    pendingAction.current = null;
    pendingLeaveRef.current = false;
    pendingHistoryBackRef.current = false;
    setPendingHash(null);
  }, [deactivateHistoryGuard]);

  return { pendingHash, hasPendingLeave: pendingHash !== null, cancelLeave, confirmLeave, requestLeave, markClean };
}

export function UnsavedChangesDialog({ guard, labels }: { guard: UnsavedChangesGuard; labels?: { title: string; description: string; continueEditing: string; discard: string } }) {
  const { t } = useI18n();
  return <Modal onClose={guard.cancelLeave} open={guard.hasPendingLeave} title={labels?.title ?? t("form.unsavedTitle")}>
    <p className="text-sm leading-6 text-brand-muted">{labels?.description ?? t("form.unsavedDescription")}</p>
    <div className="mt-5 grid grid-cols-2 gap-3">
      <Button onClick={guard.cancelLeave} type="button" variant="secondary">{labels?.continueEditing ?? t("common.cancel")}</Button>
      <Button onClick={guard.confirmLeave} type="button">{labels?.discard ?? t("form.leave")}</Button>
    </div>
  </Modal>;
}
