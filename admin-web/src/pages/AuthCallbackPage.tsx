import { useEffect, useRef } from "react";
import { completeTelegramSignIn } from "../auth/telegramCallback";
import { Spinner } from "../components/ui";
import { useI18n } from "../i18n";

// /auth/telegram: Telegram sends the browser here after the sign-in; the data goes to the API once.
export function AuthCallbackPage() {
  const { t } = useI18n();
  const started = useRef(false);
  useEffect(() => {
    if (started.current) return;
    started.current = true;
    void completeTelegramSignIn(window.location.search);
  }, []);
  return (
    <div className="grid min-h-screen place-items-center bg-brand-bg px-4">
      <Spinner label={t("callback.signingIn")} />
    </div>
  );
}
