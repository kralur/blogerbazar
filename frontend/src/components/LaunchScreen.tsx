import officialLogo from "../assets/bloggerbazar-logo-original.png";
import { useI18n } from "../i18n";

export function LaunchScreen() {
  const { t } = useI18n();

  return <main aria-label={t("firstRun.launchLoading")} className="launch-screen">
    <div className="launch-screen__content">
      <img alt={t("common.appName")} className="launch-screen__logo" src={officialLogo} />
      <h1>{t("common.appName")}</h1>
      <span aria-hidden="true" className="launch-screen__progress" />
    </div>
  </main>;
}
