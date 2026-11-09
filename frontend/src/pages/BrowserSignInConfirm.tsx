import { useCallback, useEffect, useState } from "react";
import { confirmBrowserSignIn, declineBrowserSignIn, deviceLabel, previewBrowserSignIn, type BrowserSignInPreview } from "../api/browserSessions";
import { ApiError } from "../api/client";
import { selectMarketplaceRole } from "../api/marketplace";
import { AtlasBackground } from "../components/AtlasBackground";
import { LanguageSwitcher } from "../components/LanguageSwitcher";
import { Button, Icon } from "../components/ui";
import { useI18n } from "../i18n";
import { formatShortDate } from "../lib/currency";
import { useTelegram } from "../telegram/TelegramProvider";

type State =
  | { kind: "loading" }
  | { kind: "ready"; preview: BrowserSignInPreview }
  | { kind: "busy"; preview: BrowserSignInPreview }
  | { kind: "done" }
  | { kind: "declined" }
  | { kind: "failed"; message: string };

// D64 stage B: the Mini App opened from a browser's sign-in link (startapp=web_<code>). The person checks what asks
// to sign in and taps the number the browser shows; a wrong number cancels the sign-in.
export function BrowserSignInConfirm({ code, onClose, onRoleChanged }: { code: string; onClose: () => void; onRoleChanged: () => void }) {
  const { t, language } = useI18n();
  const { haptic } = useTelegram();
  const [state, setState] = useState<State>({ kind: "loading" });

  const failure = useCallback((error: unknown) => {
    const code = error instanceof ApiError ? error.code : undefined;
    const key = code === "browser_number_mismatch" ? "browser.confirm.mismatch"
      : code === "browser_company_required" ? "browser.confirm.noCompany"
        : code === "browser_link_shared" ? "browser.confirm.sharedLink"
        : error instanceof ApiError && error.status === 404 ? "browser.confirm.gone"
          : "browser.confirm.failed";
    return t(key);
  }, [t]);

  const load = useCallback(async () => {
    setState({ kind: "loading" });
    try {
      const preview = await previewBrowserSignIn(code);
      // Somebody sent this link: cancel it at once, so their browser stops waiting.
      if (preview.eligibility === "sharedLink") void declineBrowserSignIn(code).catch(() => undefined);
      setState({ kind: "ready", preview });
    } catch (error) {
      setState({ kind: "failed", message: failure(error) });
    }
  }, [code, failure]);
  useEffect(() => { void load(); }, [load]);

  const choose = async (preview: BrowserSignInPreview, number: number) => {
    haptic.selection();
    setState({ kind: "busy", preview });
    try {
      await confirmBrowserSignIn(code, number);
      haptic.success();
      setState({ kind: "done" });
    } catch (error) {
      haptic.error();
      setState({ kind: "failed", message: failure(error) });
    }
  };

  const decline = async () => {
    haptic.warning();
    try { await declineBrowserSignIn(code); } catch { /* the code lapses on its own in two minutes */ }
    setState({ kind: "declined" });
  };

  const switchToBusiness = async (preview: BrowserSignInPreview) => {
    setState({ kind: "busy", preview });
    try {
      await selectMarketplaceRole("Business");
      onRoleChanged();
      await load();
    } catch (error) {
      setState({ kind: "failed", message: failure(error) });
    }
  };

  const preview = state.kind === "ready" || state.kind === "busy" ? state.preview : null;
  const busy = state.kind === "busy" || state.kind === "loading";
  const message = state.kind === "done" ? t("browser.confirm.done") : state.kind === "declined" ? t("browser.confirm.declinedNote") : state.kind === "failed" ? state.message : null;

  return <main className="ftue-screen ftue-screen--atlas browser-confirm">
    <AtlasBackground />
    <div className="ftue-screen__layout">
      <section className="ftue-screen__content" data-content-header>
        <div className="ftue-screen__language"><LanguageSwitcher /></div>
        <p className="ftue-screen__kicker">{t("common.appName")}</p>
        <h1 className="ftue-screen__title">{t("browser.confirm.title")}</h1>
        {preview && <div className="browser-confirm__device">
          <Icon className="h-5 w-5" name="lock" />
          <p><strong>{deviceLabel(preview, t)}</strong><span>{t("browser.confirm.requestedAt", { time: formatShortDate(preview.createdAtUtc, language, { time: true }) })}</span>{preview.companyName && <span>{t("browser.confirm.company", { name: preview.companyName })}</span>}</p>
        </div>}
        {preview?.eligibility === "ok" && <>
          <p className="ftue-screen__description ftue-screen__description--left">{t("browser.confirm.pickNumber")}</p>
          <div aria-label={t("browser.confirm.pickNumber")} className="browser-confirm__numbers" role="group">
            {preview.options.map((option) => <button className="price-tag-number" disabled={busy} key={option} onClick={() => void choose(preview, option)} type="button">{option}</button>)}
          </div>
          <p className="browser-confirm__warning">{t("browser.confirm.warning")}</p>
        </>}
        {preview?.eligibility === "switchToBusiness" && <p className="ftue-screen__description ftue-screen__description--left">{t("browser.confirm.switchHint")}</p>}
        {preview?.eligibility === "noCompany" && <p className="ftue-screen__description ftue-screen__description--left">{t("browser.confirm.noCompany")}</p>}
        {preview?.eligibility === "sharedLink" && <p className="phone-verification__note" role="alert">{t("browser.confirm.sharedLink")}</p>}
        {message && <p className="phone-verification__note" role="status">{message}</p>}
      </section>
      <div className="grid gap-2">
        {preview?.eligibility === "switchToBusiness" && <Button aria-busy={busy} className="ftue-primary-button w-full" disabled={busy} onClick={() => void switchToBusiness(preview)} type="button">{t("browser.confirm.switch")}</Button>}
        {preview && (preview.eligibility === "ok" || preview.eligibility === "switchToBusiness") && <Button className="w-full" disabled={busy} onClick={() => void decline()} type="button" variant="secondary">{t("browser.confirm.notMe")}</Button>}
        {(!preview || preview.eligibility === "noCompany" || preview.eligibility === "sharedLink") && state.kind !== "loading" && <Button className={`w-full ${state.kind === "done" ? "ftue-primary-button" : ""}`} onClick={onClose} type="button" variant={state.kind === "done" ? "primary" : "secondary"}>{t("browser.confirm.close")}</Button>}
      </div>
    </div>
  </main>;
}
