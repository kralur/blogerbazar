import { useCallback, useEffect, useRef, useState } from "react";
import { Button, Modal } from "../components/ui";
import { useI18n } from "../i18n";
import { useTelegram } from "../telegram/TelegramProvider";

export type UnsavedChangesGuard = {
  cancelLeave: () => void;
  confirmLeave: () => void;
  pendingHash: string | null;
  hasPendingLeave: boolean;
  requestLeave: (action: () => void) => void;
};

export function useUnsavedChanges(isDirty: boolean): UnsavedChangesGuard {
  const { setClosingConfirmation } = useTelegram();
  const currentHash = useRef(window.location.hash);
  const bypassGuard = useRef(false);
  const [pendingHash, setPendingHash] = useState<string | null>(null);
  const pendingAction = useRef<(() => void) | null>(null);

  useEffect(() => {
    const onHashChange = () => {
      const nextHash = window.location.hash;
      if (bypassGuard.current) {
        bypassGuard.current = false;
        currentHash.current = nextHash;
        return;
      }

      if (isDirty && nextHash !== currentHash.current) {
        setPendingHash(nextHash);
        bypassGuard.current = true;
        window.location.hash = currentHash.current;
        return;
      }

      currentHash.current = nextHash;
    };

    const onBeforeUnload = (event: BeforeUnloadEvent) => {
      if (!isDirty) return;
      event.preventDefault();
      event.returnValue = "";
    };

    window.addEventListener("hashchange", onHashChange);
    window.addEventListener("beforeunload", onBeforeUnload);
    return () => {
      window.removeEventListener("hashchange", onHashChange);
      window.removeEventListener("beforeunload", onBeforeUnload);
    };
  }, [isDirty]);

  useEffect(() => {
    setClosingConfirmation(isDirty);
    return () => setClosingConfirmation(false);
  }, [isDirty, setClosingConfirmation]);

  const cancelLeave = useCallback(() => {
    pendingAction.current = null;
    setPendingHash(null);
  }, []);
  const confirmLeave = useCallback(() => {
    if (pendingHash) {
      bypassGuard.current = true;
      window.location.hash = pendingHash;
    } else {
      pendingAction.current?.();
    }
    pendingAction.current = null;
    setPendingHash(null);
  }, [pendingHash]);

  const requestLeave = useCallback((action: () => void) => {
    if (!isDirty) {
      action();
      return;
    }
    pendingAction.current = action;
    setPendingHash("");
  }, [isDirty]);

  return { pendingHash, hasPendingLeave: pendingHash !== null, cancelLeave, confirmLeave, requestLeave };
}

export function UnsavedChangesDialog({ guard }: { guard: UnsavedChangesGuard }) {
  const { t } = useI18n();
  return <Modal onClose={guard.cancelLeave} open={guard.hasPendingLeave} title={t("form.unsavedTitle")}>
    <p className="text-sm leading-6 text-brand-muted">{t("form.unsavedDescription")}</p>
    <div className="mt-5 grid grid-cols-2 gap-3">
      <Button onClick={guard.cancelLeave} type="button" variant="secondary">{t("common.cancel")}</Button>
      <Button onClick={guard.confirmLeave} type="button">{t("form.leave")}</Button>
    </div>
  </Modal>;
}
