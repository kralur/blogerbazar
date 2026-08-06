import { Button, Icon } from "../components/ui";
import { useI18n } from "../i18n";
import officialLogo from "../assets/bloggerbazar-logo-original.png";

export function TelegramAuthorization({ isTelegram, loading, failed, onContinue }: { isTelegram: boolean; loading: boolean; failed: boolean; onContinue: () => void }) {
  const { t } = useI18n();
  const title = failed ? t("firstRun.authFailedTitle") : isTelegram ? t("firstRun.telegramTitle") : t("firstRun.telegramOutsideTitle");
  const subtitle = failed ? t("firstRun.authFailedSubtitle") : isTelegram ? t("firstRun.telegramSubtitle") : t("firstRun.telegramOutsideSubtitle");
  const action = failed || !isTelegram ? t("common.retry") : t("firstRun.continueTelegram");
  return <main className="ftue-screen">
    <div className="ftue-screen__layout">
      <section className="ftue-screen__content">
        <img alt={t("common.appName")} className="ftue-screen__logo ftue-screen__logo--compact" src={officialLogo} />
        <h1 className="ftue-screen__title">{title}</h1>
        <p className="ftue-screen__description ftue-screen__description--left">{subtitle}</p>
        <div className="ftue-screen__info"><Icon className="h-5 w-5" name="lock" /><p>{t("firstRun.telegramPrivacy")}</p></div>
      </section>
      <Button aria-busy={loading} className="ftue-primary-button w-full" disabled={loading} onClick={onContinue} type="button" variant="secondary"><Icon name="refresh" />{loading ? t("firstRun.authorizing") : action}</Button>
    </div>
  </main>;
}
