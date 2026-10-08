import { useI18n } from "../i18n";
import { useTelegram } from "../telegram/TelegramProvider";

// The @username comes from Telegram's signed launch data; the server ignores what the form sends.
export function telegramHandle(username?: string | null) {
  const value = username?.trim().replace(/^@+/, "");
  return value && /^[A-Za-z0-9_]{5,32}$/.test(value) ? `@${value}` : null;
}

export function useTelegramHandle() {
  return telegramHandle(useTelegram().user?.username);
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
