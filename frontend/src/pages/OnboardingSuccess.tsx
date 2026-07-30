import { Button, Icon } from "../components/ui";
import { useI18n } from "../i18n";

export function OnboardingSuccess({ onContinue }: { onContinue: () => void }) {
  const { t } = useI18n();
  return <main className="screen flex min-h-screen flex-col px-5 pb-8 pt-10 text-center">
    <div className="mx-auto grid h-24 w-24 place-items-center rounded-full bg-brand-gradient text-white shadow-glow"><Icon className="h-11 w-11" name="check" /></div>
    <div className="my-auto"><p className="text-sm font-bold text-brand-blue">{t("common.appName")}</p><h1 className="mt-3 text-3xl font-extrabold tracking-tight">{t("firstRun.successTitle")}</h1><p className="mx-auto mt-3 max-w-sm text-sm leading-6 text-brand-muted">{t("firstRun.successSubtitle")}</p></div>
    <Button className="w-full" onClick={onContinue} type="button"><Icon name="home" />{t("firstRun.goHome")}</Button>
  </main>;
}
