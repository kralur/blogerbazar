import { useEffect, useRef, useState } from "react";
import { ApiError } from "../api/client";
import { getAdminWebConfig, getAdminWebSession } from "../api/session";
import { callbackPath, type SignInError } from "../auth/telegramCallback";
import { Alert, BrandMark, Card, Spinner } from "../components/ui";
import { Preferences } from "../components/Preferences";
import { useI18n, type Language } from "../i18n";
import { browser } from "../lib/browser";

export const telegramWidgetUrl = "https://telegram.org/js/telegram-widget.js?22";
const knownErrors: SignInError[] = ["denied", "expired", "rate", "config", "failed"];

export function errorFromQuery(search: string): SignInError | null {
  const value = new URLSearchParams(search).get("error");
  return knownErrors.find((error) => error === value) ?? null;
}

type State = { status: "loading" } | { status: "ready"; botUsername: string } | { status: "error"; error: SignInError };

// /login (its own page, login.html): the only document where the Telegram widget script runs. The widget sends
// the browser to /auth/telegram with signed data (data-auth-url), so no inline callback (and no eval) is needed.
export function LoginPage() {
  const { t, language } = useI18n();
  const [state, setState] = useState<State>({ status: "loading" });
  const queryError = errorFromQuery(window.location.search);

  useEffect(() => {
    let cancelled = false;
    void (async () => {
      try {
        await getAdminWebSession();
        if (!cancelled) browser.replace("/");
        return;
      } catch {
        // No session yet: show the sign-in.
      }

      try {
        const config = await getAdminWebConfig();
        if (cancelled) return;
        setState(config.botUsername ? { status: "ready", botUsername: config.botUsername } : { status: "error", error: "config" });
      } catch (error) {
        if (!cancelled) setState({ status: "error", error: error instanceof ApiError && error.status === 404 ? "config" : "failed" });
      }
    })();
    return () => {
      cancelled = true;
    };
  }, []);

  const shownError = state.status === "error" ? state.error : queryError;
  return (
    <div className="flex min-h-screen flex-col bg-brand-bg px-4 py-8 text-brand-ink">
      <div className="mx-auto flex w-full max-w-md flex-1 flex-col justify-center gap-6">
        <div className="flex items-center gap-3">
          <BrandMark />
          <div>
            <p className="text-base font-extrabold">{t("app.name")}</p>
            <p className="text-xs text-brand-muted">{t("app.admin")}</p>
          </div>
        </div>
        <Card className="space-y-5">
          <div className="space-y-2">
            <h1 className="text-2xl font-extrabold">{t("login.title")}</h1>
            <p className="text-sm leading-6 text-brand-muted">{t("login.lead")}</p>
          </div>
          {shownError && <Alert>{errorText(shownError, t)}</Alert>}
          {state.status === "loading" && <Spinner label={t("login.loading")} />}
          {state.status === "ready" && <TelegramLoginButton botUsername={state.botUsername} language={language} />}
          <p className="text-xs leading-5 text-brand-muted">{t("login.hint")}</p>
        </Card>
        <Preferences />
      </div>
    </div>
  );
}

function errorText(error: SignInError, t: (key: string) => string) {
  switch (error) {
    case "denied": return t("login.error.denied");
    case "expired": return t("login.error.expired");
    case "rate": return t("login.error.rate");
    case "config": return t("login.error.config");
    default: return t("login.error.failed");
  }
}

function TelegramLoginButton({ botUsername, language }: { botUsername: string; language: Language }) {
  const container = useRef<HTMLDivElement>(null);
  useEffect(() => {
    const element = container.current;
    if (!element) return;
    const script = document.createElement("script");
    script.async = true;
    script.src = telegramWidgetUrl;
    script.dataset.telegramLogin = botUsername;
    script.dataset.size = "large";
    script.dataset.radius = "12";
    script.dataset.lang = language;
    script.dataset.authUrl = `${window.location.origin}${callbackPath}`;
    element.replaceChildren(script);
    return () => element.replaceChildren();
  }, [botUsername, language]);
  return <div ref={container} data-testid="telegram-login" className="flex min-h-[48px] items-center justify-center" />;
}
