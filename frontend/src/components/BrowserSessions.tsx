import { useCallback, useEffect, useState } from "react";
import { deviceLabel, endAllBrowserSessions, endBrowserSession, getBrowserSessions, type BrowserSession } from "../api/browserSessions";
import { useI18n } from "../i18n";
import { isBrowserSession } from "../lib/browserSession";
import { formatShortDate } from "../lib/currency";
import { useTelegram } from "../telegram/TelegramProvider";
import { Button, Modal } from "./ui";

// D64 stage B, Settings: the browsers signed in to this person's company work, and the way out of each or all.
// onSignedOut: this very browser's session has just ended.
export function BrowserSessions({ onSignedOut }: { onSignedOut?: () => void }) {
  const { t, language } = useI18n();
  const { haptic } = useTelegram();
  const [sessions, setSessions] = useState<BrowserSession[] | null>(null);
  const [failed, setFailed] = useState(false);
  const [confirmAll, setConfirmAll] = useState(false);
  const [busy, setBusy] = useState(false);
  const inBrowser = isBrowserSession();

  const load = useCallback(async () => {
    try {
      setSessions(await getBrowserSessions());
      setFailed(false);
    } catch {
      setFailed(true);
    }
  }, []);
  useEffect(() => { void load(); }, [load]);

  const endOne = async (session: BrowserSession) => {
    haptic.warning();
    setBusy(true);
    try {
      await endBrowserSession(session.id);
      if (session.isCurrent) { onSignedOut?.(); return; }
      await load();
    } catch {
      setFailed(true);
    } finally {
      setBusy(false);
    }
  };

  const endAll = async () => {
    setBusy(true);
    try {
      await endAllBrowserSessions();
      haptic.success();
      setConfirmAll(false);
      if (inBrowser) { onSignedOut?.(); return; }
      await load();
    } catch {
      setFailed(true);
    } finally {
      setBusy(false);
    }
  };

  const address = typeof window === "undefined" ? "" : window.location.host;
  return <section aria-labelledby="browser-sessions-title" className="settings-list browser-sessions">
    <div className="settings-row settings-row--stacked">
      <span className="settings-row__text">
        <strong id="browser-sessions-title">{t("browser.sessions.title")}</strong>
        <span>{inBrowser ? t("browser.sessions.hintInBrowser") : t("browser.sessions.hint", { address })}</span>
      </span>
      {failed && <p className="text-sm text-brand-muted" role="status">{t("browser.sessions.failed")}</p>}
      {sessions && sessions.length === 0 && <p className="text-sm text-brand-muted">{t("browser.sessions.none")}</p>}
      {sessions && sessions.length > 0 && <ul className="browser-sessions__list">
        {sessions.map((session) => <li className="browser-sessions__item" key={session.id}>
          <span className="settings-row__text">
            <strong>{deviceLabel(session, t)}{session.isCurrent && <span className="browser-sessions__current">{t("browser.sessions.current")}</span>}</strong>
            <span>{t("browser.sessions.signedIn", { time: formatShortDate(session.createdAtUtc, language, { time: true }) })}</span>
            <span>{t("browser.sessions.lastSeen", { time: formatShortDate(session.lastSeenAtUtc, language, { time: true }) })}</span>
          </span>
          <Button className="browser-sessions__end" disabled={busy} onClick={() => void endOne(session)} type="button" variant="secondary">{t("browser.sessions.end")}</Button>
        </li>)}
      </ul>}
      {sessions && sessions.length > 0 && <Button className="w-full" disabled={busy} onClick={() => { haptic.warning(); setConfirmAll(true); }} type="button" variant="danger">{t("browser.sessions.endAll")}</Button>}
    </div>
    <Modal onClose={() => { if (!busy) setConfirmAll(false); }} open={confirmAll} title={t("browser.sessions.endAllTitle")}>
      <p className="text-sm leading-6 text-brand-muted">{t("browser.sessions.endAllDescription")}</p>
      <div className="mt-5 modal-actions">
        <Button disabled={busy} onClick={() => setConfirmAll(false)} type="button" variant="secondary">{t("common.cancel")}</Button>
        <Button aria-busy={busy} disabled={busy} onClick={() => void endAll()} type="button" variant="danger">{t("browser.sessions.endAll")}</Button>
      </div>
    </Modal>
  </section>;
}
