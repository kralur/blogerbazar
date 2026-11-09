import { useCallback, useEffect, useRef, useState } from "react";
import { checkBrowserSignIn, startBrowserSignIn, type BrowserSignInStart } from "../api/browserSessions";
import { getApiErrorMessage } from "../api/client";
import { AtlasBackground } from "../components/AtlasBackground";
import { LanguageSwitcher } from "../components/LanguageSwitcher";
import { QrCode } from "../components/QrCode";
import { Button, Icon } from "../components/ui";
import { useI18n } from "../i18n";

const checkEveryMs = 3000;

type State =
  | { kind: "idle"; error?: string }
  | { kind: "starting" }
  | { kind: "waiting"; request: BrowserSignInStart }
  | { kind: "expired" }
  | { kind: "declined" };

function secondsLeft(expiresAtUtc: string) {
  return Math.max(0, Math.ceil((new Date(expiresAtUtc).getTime() - Date.now()) / 1000));
}

// D64 stage B: the page outside Telegram. A company member signs this browser in by opening the link in Telegram
// (phone or Telegram Desktop) and tapping the number shown here; the page checks back every few seconds.
export function BrowserSignIn({ onSignedIn }: { onSignedIn: () => void }) {
  const { t } = useI18n();
  const [state, setState] = useState<State>({ kind: "idle" });
  const [remaining, setRemaining] = useState(0);
  const [copied, setCopied] = useState(false);
  const signedIn = useRef(false);

  const start = useCallback(async () => {
    setState({ kind: "starting" });
    setCopied(false);
    try {
      const request = await startBrowserSignIn();
      setRemaining(secondsLeft(request.expiresAtUtc));
      setState({ kind: "waiting", request });
    } catch (error) {
      setState({ kind: "idle", error: getApiErrorMessage(error, t("browser.startFailed")) });
    }
  }, [t]);

  const waitingRequest = state.kind === "waiting" ? state.request : null;
  useEffect(() => {
    if (!waitingRequest) return;
    let active = true;
    const tick = window.setInterval(() => {
      const left = secondsLeft(waitingRequest.expiresAtUtc);
      setRemaining(left);
      if (left === 0) setState({ kind: "expired" });
    }, 1000);
    const poll = window.setInterval(() => {
      void checkBrowserSignIn().then((result) => {
        if (!active || signedIn.current) return;
        if (result.status === "approved") {
          signedIn.current = true;
          onSignedIn();
        } else if (result.status === "declined") setState({ kind: "declined" });
        else if (result.status === "expired") setState({ kind: "expired" });
      }).catch(() => undefined);
    }, checkEveryMs);
    return () => {
      active = false;
      window.clearInterval(tick);
      window.clearInterval(poll);
    };
  }, [onSignedIn, waitingRequest]);

  const copyLink = async () => {
    if (!waitingRequest) return;
    try {
      await navigator.clipboard.writeText(waitingRequest.link);
      setCopied(true);
    } catch { /* the link is on the button as well */ }
  };

  const minutes = Math.floor(remaining / 60);
  const seconds = String(remaining % 60).padStart(2, "0");
  const note = state.kind === "expired" ? t("browser.expired") : state.kind === "declined" ? t("browser.declined") : state.kind === "idle" ? state.error : undefined;

  return <main className="ftue-screen ftue-screen--atlas browser-sign-in">
    <AtlasBackground />
    <div className="ftue-screen__layout">
      <section className="ftue-screen__content" data-content-header>
        <div className="ftue-screen__language"><LanguageSwitcher /></div>
        <p className="ftue-screen__kicker">{t("common.appName")}</p>
        <h1 className="ftue-screen__title">{t("browser.title")}</h1>
        {waitingRequest ? <ol className="browser-sign-in__steps">
          <li>
            <span className="browser-sign-in__step-title">{t("browser.stepOpen")}</span>
            {/* A computer shows the QR code for the phone's camera; a phone has nothing to scan it with. */}
            <span className="browser-sign-in__qr"><QrCode className="browser-sign-in__qr-code" label={t("browser.qrLabel")} value={waitingRequest.link} /></span>
            <span className="browser-sign-in__step-hint browser-sign-in__step-hint--qr">{t("browser.stepOpenHintQr")}</span>
            <span className="browser-sign-in__step-hint browser-sign-in__step-hint--link">{t("browser.stepOpenHint")}</span>
            <span className="browser-sign-in__link-actions">
              <a className="ds-button ds-button--primary ftue-primary-button tap-target inline-flex items-center justify-center gap-2 px-5" href={waitingRequest.link} rel="noopener noreferrer" target="_blank"><Icon name="telegram" />{t("browser.openTelegram")}</a>
              <button className="browser-sign-in__copy" onClick={() => void copyLink()} type="button"><Icon className="h-4 w-4" name={copied ? "check" : "copy"} />{copied ? t("browser.linkCopied") : t("browser.copyLink")}</button>
            </span>
          </li>
          <li>
            <span className="browser-sign-in__step-title">{t("browser.stepNumber")}</span>
            <span aria-label={t("browser.numberLabel", { number: waitingRequest.number })} className="price-tag-number" role="img">{waitingRequest.number}</span>
            <span className="browser-sign-in__step-hint" role="status">{t("browser.waiting", { time: `${minutes}:${seconds}` })}</span>
          </li>
        </ol> : <p className="ftue-screen__description ftue-screen__description--left">{t("browser.subtitle")}</p>}
        <div className="ftue-screen__info"><Icon className="h-5 w-5" name="lock" /><p>{t("browser.privacy")}</p></div>
        {note && <p className="phone-verification__note" role="status">{note}</p>}
      </section>
      {!waitingRequest && <Button aria-busy={state.kind === "starting"} className="ftue-primary-button w-full" disabled={state.kind === "starting"} onClick={() => void start()} type="button">
        <Icon name={state.kind === "expired" || state.kind === "declined" ? "refresh" : "telegram"} />
        {state.kind === "starting" ? t("browser.starting") : state.kind === "expired" || state.kind === "declined" ? t("browser.again") : t("browser.signIn")}
      </Button>}
    </div>
  </main>;
}
