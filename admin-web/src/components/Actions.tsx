import { useCallback, useEffect, useId, useRef, useState, type ReactNode } from "react";
import { ApiError } from "../api/client";
import { useI18n } from "../i18n";
import { browser } from "../lib/browser";
import { Alert, Button } from "./ui";

// A change the admin asked for: what the dialog says, and what runs on "confirm". Dangerous changes always go
// through this dialog; the server checks the rights again either way.
export type AdminAction = {
  title: string;
  text: string;
  confirmLabel: string;
  danger?: boolean;
  // Optional extra input inside the dialog (a note, a status choice); its value is read by run().
  body?: ReactNode;
  // false keeps the dialog open (the input is not valid yet).
  canConfirm?: boolean;
  run: () => Promise<unknown>;
  done: string;
};

// The dialog keeps keyboard focus inside itself, closes on Escape and gives focus back to where it was.
export function ConfirmDialog({ action, busy, error, onConfirm, onCancel }: { action: AdminAction; busy: boolean; error: string | null; onConfirm: () => void; onCancel: () => void }) {
  const { t } = useI18n();
  const titleId = useId();
  const textId = useId();
  const panel = useRef<HTMLDivElement>(null);
  const cancel = useRef<HTMLButtonElement>(null);

  useEffect(() => {
    const previous = document.activeElement as HTMLElement | null;
    // A dangerous change starts on "Cancel", so a stray Enter does not confirm it.
    (action.body ? panel.current?.querySelector<HTMLElement>("input, select, textarea") : cancel.current)?.focus();
    return () => previous?.focus?.();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const onKeyDown = (event: React.KeyboardEvent) => {
    if (event.key === "Escape" && !busy) {
      event.stopPropagation();
      onCancel();
      return;
    }
    if (event.key !== "Tab" || !panel.current) return;
    const focusable = [...panel.current.querySelectorAll<HTMLElement>("button, input, select, textarea, a[href]")].filter((element) => !element.hasAttribute("disabled"));
    if (focusable.length === 0) return;
    const first = focusable[0];
    const last = focusable[focusable.length - 1];
    if (event.shiftKey && document.activeElement === first) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && document.activeElement === last) {
      event.preventDefault();
      first.focus();
    }
  };

  return (
    <div className="fixed inset-0 z-50 grid place-items-center bg-[color:var(--bb-overlay)] p-4" onKeyDown={onKeyDown}>
      <div ref={panel} role="dialog" aria-modal="true" aria-labelledby={titleId} aria-describedby={textId}
        className="w-full max-w-md space-y-4 rounded-[var(--bb-radius-overlay)] border border-brand-line bg-brand-surface p-5 text-brand-ink shadow-soft sm:p-6">
        <h2 id={titleId} className="text-lg font-extrabold">{action.title}</h2>
        <p id={textId} className="text-sm leading-6 text-brand-muted">{action.text}</p>
        {action.body}
        {error && <Alert>{error}</Alert>}
        <div className="flex flex-wrap justify-end gap-2">
          <Button ref={cancel} onClick={onCancel} disabled={busy}>{t("action.cancel")}</Button>
          <Button variant={action.danger ? "danger" : "primary"} onClick={onConfirm} disabled={busy || action.canConfirm === false}>
            {busy ? t("action.working") : action.confirmLabel}
          </Button>
        </div>
      </div>
    </div>
  );
}

// One place for every page: ask(action) opens the dialog, the result shows as a short line, and the page reloads its
// list after a change. A session that ended on the way goes to the sign-in page.
export function useActions(onChanged: () => void) {
  const { t } = useI18n();
  const [action, setAction] = useState<AdminAction | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  const errorText = useCallback((reason: unknown) => {
    if (!(reason instanceof ApiError)) return t("action.error.failed");
    if (reason.status === 403) return t("action.error.denied");
    if (reason.status === 404) return t("action.error.missing");
    if (reason.status === 409) return t("action.error.conflict");
    if (reason.status === 422 || reason.status === 400) return t("action.error.invalid");
    if (reason.status === 429) return t("action.error.rate");
    return t("action.error.failed");
  }, [t]);

  const ask = useCallback((next: AdminAction) => {
    setError(null);
    setNotice(null);
    setAction(next);
  }, []);

  const confirm = async () => {
    if (!action) return;
    setBusy(true);
    setError(null);
    try {
      await action.run();
      setNotice(action.done);
      setAction(null);
      onChanged();
    } catch (reason) {
      if (reason instanceof ApiError && reason.status === 401) {
        browser.replace("/login?error=expired");
        return;
      }
      setError(errorText(reason));
    } finally {
      setBusy(false);
    }
  };

  const dialog = action
    ? <ConfirmDialog action={action} busy={busy} error={error} onConfirm={() => void confirm()} onCancel={() => setAction(null)} />
    : null;
  const status = notice
    ? <p role="status" className="rounded-[var(--bb-radius-control)] bg-[color:var(--bb-success-subtle)] px-4 py-3 text-sm font-medium text-[color:var(--bb-success-on-subtle)]">{notice}</p>
    : null;
  return { ask, dialog, status };
}
