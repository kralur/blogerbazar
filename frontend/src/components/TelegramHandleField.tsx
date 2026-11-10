import { useI18n } from "../i18n";
import { useTelegram } from "../telegram/TelegramProvider";
import { browserSessionUsername } from "../lib/browserSession";

// The @username comes from Telegram's signed launch data; the server ignores what the form sends.
export function telegramHandle(username?: string | null) {
  const value = username?.trim().replace(/^@+/, "");
  return value && /^[A-Za-z0-9_]{5,32}$/.test(value) ? `@${value}` : null;
}

export function useTelegramHandle() {
  // On the website the account's username comes from the server (the browser has no Telegram launch data).
  return telegramHandle(useTelegram().user?.username ?? browserSessionUsername());
}

// Read-only on purpose: a typed username would let anyone pose as someone else.
export function TelegramHandleField() {
  const { t } = useI18n();
  const handle = useTelegramHandle();
  return <div className="telegram-handle-field" data-wizard-field="username">
    <span className="telegram-handle-field__label">{t("form.telegramUsername")}</span>
    <strong className="telegram-handle-field__value">{handle ?? t("form.telegramNoUsername")}</strong>
    <p className="wizard-field-helper">{t(handle ? "form.telegramFromAccount" : "form.telegramNoUsernameHelper")}</p>
  </div>;
}
