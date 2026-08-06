import { Button } from "../components/ui";
import { useI18n } from "../i18n";
import officialLogo from "../assets/bloggerbazar-logo-original.png";

export function Welcome({ onContinue }: { onContinue: () => void }) {
  const { t } = useI18n();
  return <main className="ftue-screen"><div className="ftue-screen__layout"><section className="ftue-screen__content text-center"><div className="ftue-screen__brand-block"><img alt={t("common.appName")} className="ftue-screen__logo" src={officialLogo} /><h1 className="ftue-screen__brand">{t("common.appName")}</h1><p className="ftue-screen__description">{t("firstRun.welcomeSubtitle")}</p></div></section><Button aria-label={t("firstRun.start")} className="ftue-primary-button w-full" onClick={onContinue} type="button" variant="secondary">{t("firstRun.start")}</Button></div></main>;
}
