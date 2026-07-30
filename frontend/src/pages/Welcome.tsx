import { Button, Icon } from "../components/ui";
import { useI18n } from "../i18n";

export function Welcome({ onContinue }: { onContinue: () => void }) {
  const { t } = useI18n();
  return <main className="screen relative flex min-h-screen overflow-hidden px-5 pb-8 pt-10">
    <div aria-hidden="true" className="absolute -left-20 top-8 h-72 w-72 rounded-full bg-blue-300/35 blur-3xl" />
    <div aria-hidden="true" className="absolute -right-20 bottom-24 h-72 w-72 rounded-full bg-cyan-200/50 blur-3xl" />
    <div className="relative flex w-full flex-col">
      <div aria-label={t("common.appName")} className="grid h-24 w-24 place-items-center rounded-[32px] bg-brand-gradient text-5xl font-light text-white shadow-glow">B</div>
      <div className="mt-auto rounded-[32px] border border-white/80 bg-white/75 p-6 shadow-card backdrop-blur-xl">
        <p className="text-sm font-bold text-brand-blue">{t("common.appName")}</p>
        <h1 className="mt-3 text-3xl font-extrabold leading-tight tracking-tight">{t("firstRun.welcomeTitle")}</h1>
        <p className="mt-3 text-sm leading-6 text-brand-muted">{t("firstRun.welcomeSubtitle")}</p>
      </div>
      <Button aria-label={t("firstRun.start")} className="mt-5 w-full" onClick={onContinue} type="button"><Icon name="home" />{t("firstRun.start")}</Button>
    </div>
  </main>;
}
