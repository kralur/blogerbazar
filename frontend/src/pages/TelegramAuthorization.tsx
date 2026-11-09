import { AtlasBackground } from "../components/AtlasBackground";
import { SeasonArt } from "../components/SeasonArt";
import { Button, Icon } from "../components/ui";
import { LanguageSwitcher } from "../components/LanguageSwitcher";
import { useI18n } from "../i18n";

export function TelegramAuthorization({ isTelegram, loading, failed, onContinue }: { isTelegram: boolean; loading: boolean; failed: boolean; onContinue: () => void }) {
  const { t } = useI18n();
  const title = failed ? t("firstRun.authFailedTitle") : isTelegram ? t("firstRun.telegramTitle") : t("firstRun.telegramOutsideTitle");
  const subtitle = failed ? t("firstRun.authFailedSubtitle") : isTelegram ? t("firstRun.telegramSubtitle") : t("firstRun.telegramOutsideSubtitle");
  const action = failed || !isTelegram ? t("common.retry") : t("firstRun.continueTelegram");
  const showRetryIcon = !isTelegram && !loading;
  return <main className="ftue-screen ftue-screen--atlas">
    <AtlasBackground />
    <SeasonArt variant="falling" />
    <div className="ftue-screen__layout">
      <section className="ftue-screen__content" data-content-header>
        <div className="ftue-screen__language"><LanguageSwitcher /></div>
        <p className="ftue-screen__kicker">{t("common.appName")}</p>
        <h1 className="ftue-screen__title">{title}</h1>
        <p className="ftue-screen__description ftue-screen__description--left">{subtitle}</p>
        <div className="ftue-screen__info"><Icon className="h-5 w-5" name="lock" /><p>{t("firstRun.telegramPrivacy")}</p></div>
      </section>
      <Button aria-busy={loading} className="ftue-primary-button w-full" disabled={loading} onClick={onContinue} type="button" variant="secondary">{showRetryIcon && <Icon name="refresh" />}{loading ? t("firstRun.authorizing") : action}</Button>
    </div>
  </main>;
}
