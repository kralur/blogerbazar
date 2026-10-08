import { useState } from "react";
import { getCurrentPlatformUser } from "../api/marketplace";
import { LanguageSwitcher } from "../components/LanguageSwitcher";
import { Button, Icon } from "../components/ui";
import { useI18n } from "../i18n";
import { telegramBridge } from "../telegram/TelegramProvider";
import officialLogo from "../assets/bloggerbazar-logo-original.png";

const pollAttempts = 8;
const pollDelayMs = 1500;

// The number travels Telegram → bot → server, so after sharing the page waits until the server has it.
async function waitForVerifiedPhone() {
  for (let attempt = 0; attempt < pollAttempts; attempt += 1) {
    try {
      const user = await getCurrentPlatformUser();
      if (user.verifiedPhone) return true;
    } catch { /* keep waiting; a network hiccup is not a refusal */ }
    await new Promise((resolve) => window.setTimeout(resolve, pollDelayMs));
  }
  return false;
}

// Required step: a profile's phone is only the one Telegram confirmed for this account (D41).
export function PhoneVerification({ onVerified }: { onVerified: () => void }) {
  const { t } = useI18n();
  const [state, setState] = useState<"idle" | "waiting" | "declined" | "failed">("idle");

  const check = async () => {
    setState("waiting");
    if (await waitForVerifiedPhone()) onVerified();
    else setState("failed");
  };
  const share = async () => {
    setState("waiting");
    if (!(await telegramBridge.requestContact())) {
      // Older clients cannot open the dialog; the bot's /phone button does the same.
      setState("declined");
      return;
    }
    await check();
  };

  const note = state === "declined" ? t("phone.declined") : state === "failed" ? t("phone.notReceived") : null;
  return <main className="ftue-screen">
    <div className="ftue-screen__layout">
      <section className="ftue-screen__content" data-content-header>
        <div className="ftue-screen__language"><LanguageSwitcher /></div>
        <img alt={t("common.appName")} className="ftue-screen__logo ftue-screen__logo--compact" src={officialLogo} />
        <h1 className="ftue-screen__title">{t("phone.title")}</h1>
        <p className="ftue-screen__description ftue-screen__description--left">{t("phone.subtitle")}</p>
        <div className="ftue-screen__info"><Icon className="h-5 w-5" name="lock" /><p>{t("phone.privacy")}</p></div>
        {note && <p className="phone-verification__note" role="status">{note}</p>}
      </section>
      <div className="grid gap-2">
        <Button aria-busy={state === "waiting"} className="ftue-primary-button w-full" disabled={state === "waiting"} onClick={() => void share()} type="button"><Icon name="phone" />{state === "waiting" ? t("phone.waiting") : t("phone.share")}</Button>
        {(state === "declined" || state === "failed") && <Button className="w-full" onClick={() => void check()} type="button" variant="secondary">{t("phone.sentInBot")}</Button>}
      </div>
    </div>
  </main>;
}
