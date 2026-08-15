import { Button, Icon } from "../components/ui";
import { LanguageSwitcher } from "../components/LanguageSwitcher";
import { useI18n } from "../i18n";
import officialLogo from "../assets/bloggerbazar-logo-original.png";

export function OnboardingSuccess({ onContinue }: { onContinue: () => void }) {
  const { t } = useI18n();
  const mediaWarning = window.sessionStorage.getItem("bloggerbazar.onboarding.media-warning");
  const continueToHome = () => {
    window.sessionStorage.removeItem("bloggerbazar.onboarding.media-warning");
    onContinue();
  };
  return <main className="ftue-screen ftue-success">
    <div className="ftue-screen__layout">
      <section className="ftue-screen__content ftue-success__content" data-content-header>
        <div className="ftue-success__language"><LanguageSwitcher /></div>
        <img alt={t("common.appName")} className="ftue-success__logo" src={officialLogo} />
        <span aria-hidden="true" className="ftue-success__indicator"><Icon name="check" /></span>
        <h1 className="ftue-success__title">{t("firstRun.successTitle")}</h1>
        <p className="ftue-success__description">{t("firstRun.successSubtitle")}</p>
        {mediaWarning && <p className="ftue-success__notice" role="status">{mediaWarning}</p>}
      </section>
      <Button className="ftue-primary-button w-full" onClick={continueToHome} type="button" variant="secondary">{t("firstRun.goHome")}</Button>
    </div>
  </main>;
}
