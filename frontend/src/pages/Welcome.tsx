import { BloggerBazarLogo } from "../components/BloggerBazarLogo";
import { Button, Icon } from "../components/ui";
import { useI18n } from "../i18n";

export function Welcome({ onContinue }: { onContinue: () => void }) {
  const { t } = useI18n();
  return <main className="first-run-screen relative"><div aria-hidden="true" className="absolute -left-20 top-8 h-72 w-72 rounded-full bg-blue-300/35 blur-3xl" /><div aria-hidden="true" className="absolute -right-20 bottom-24 h-72 w-72 rounded-full bg-cyan-200/50 blur-3xl" /><div className="relative flex min-h-0 flex-1 flex-col"><section className="first-run-screen__hero"><BloggerBazarLogo size={136} /><div className="mt-6"><p className="text-3xl font-extrabold tracking-tight">{t("common.appName")}</p><p className="mt-3 text-sm leading-6 text-brand-muted">{t("firstRun.welcomeSubtitle")}</p></div><div className="mt-7 w-full rounded-[32px] border border-white/80 bg-white/75 p-6 text-left shadow-card backdrop-blur-xl"><h1 className="text-xl font-extrabold leading-tight tracking-tight">{t("firstRun.welcomeTitle")}</h1><p className="mt-3 text-sm leading-6 text-brand-muted">{t("firstRun.welcomeCardDescription")}</p></div></section><Button aria-label={t("firstRun.start")} className="mt-6 w-full shrink-0" onClick={onContinue} type="button"><Icon name="home" />{t("firstRun.start")}</Button></div></main>;
}
