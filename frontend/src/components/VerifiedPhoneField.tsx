import { useEffect, useState } from "react";
import { getCurrentPlatformUser } from "../api/marketplace";
import { useI18n } from "../i18n";

let cachedPhone: string | null | undefined;

// The phone shared from Telegram (D41). The server puts it on the profile itself; the form only shows it.
export function useVerifiedPhone() {
  const [phone, setPhone] = useState<string | null | undefined>(cachedPhone);
  useEffect(() => {
    let active = true;
    Promise.resolve().then(() => getCurrentPlatformUser()).then((user) => {
      cachedPhone = user?.verifiedPhone ?? null;
      if (active) setPhone(cachedPhone);
    }).catch(() => { if (active) setPhone((current) => current ?? null); });
    return () => { active = false; };
  }, []);
  return phone;
}

export function VerifiedPhoneField({ phone }: { phone: string | null | undefined }) {
  const { t } = useI18n();
  return <div className="telegram-handle-field" data-wizard-field="phone">
    <span className="telegram-handle-field__label">{t("common.phone")}</span>
    <strong className="telegram-handle-field__value">{phone === undefined ? t("common.loading") : phone ?? t("phone.missing")}</strong>
    <p className="wizard-field-helper">{t("phone.changeHint")}</p>
  </div>;
}
