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

export function useUnsavedChanges(isDirty: boolean): UnsavedChangesGuard {
  const { setClosingConfirmation } = useTelegram();
  const currentHash = useRef(window.location.hash);
  const bypassGuard = useRef(false);
  const dirtyRef = useRef(isDirty);
  dirtyRef.current = isDirty;
  const pendingLeaveRef = useRef(false);
  const [pendingHash, setPendingHash] = useState<string | null>(null);
  const pendingAction = useRef<(() => void) | null>(null);
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
    const onHashChange = () => {
      const nextHash = window.location.hash;
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

    const onBeforeUnload = (event: BeforeUnloadEvent) => {
      if (!dirtyRef.current) return;
      event.preventDefault();
      event.returnValue = "";
    };

    const onDocumentClick = (event: MouseEvent) => {
      if (!dirtyRef.current || pendingLeaveRef.current || event.defaultPrevented || event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;
      const target = event.target;
      if (!(target instanceof Element)) return;
      const link = target.closest<HTMLAnchorElement>('a[href^="#/"]');
      const href = link?.getAttribute("href");
      if (!href || href === currentHash.current) return;
      event.preventDefault();
      requestLeave(() => { window.location.hash = href.slice(1); });
    };

    window.addEventListener("hashchange", onHashChange);
    window.addEventListener("beforeunload", onBeforeUnload);
    const onGuardedNavigation = (event: Event) => {
      const destination = (event as CustomEvent<GuardedNavigationDetail>).detail.destination;
      if (!dirtyRef.current || destination === currentHash.current) return;
      event.preventDefault();
      if (pendingLeaveRef.current) return;
      requestLeave(() => { window.location.hash = destination; });
    };
    window.addEventListener(guardedNavigationEvent, onGuardedNavigation);
    document.addEventListener("click", onDocumentClick, true);
    return () => {
      window.removeEventListener("hashchange", onHashChange);
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
    setPendingHash(null);
  }, []);
  const confirmLeave = useCallback(() => {
    const action = pendingAction.current;
    if (action) {
      bypassGuard.current = true;
      dirtyRef.current = false;
      action();
    } else if (pendingHash) {
      bypassGuard.current = true;
      window.location.hash = pendingHash;
    }
    pendingAction.current = null;
    pendingLeaveRef.current = false;
    setPendingHash(null);
  }, [pendingHash]);

  const markClean = useCallback(() => {
    dirtyRef.current = false;
    pendingAction.current = null;
    pendingLeaveRef.current = false;
    setPendingHash(null);
  }, []);

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
