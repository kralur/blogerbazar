import { Button, Card, Icon } from "../components/ui";
import { useI18n } from "../i18n";

export function TelegramAuthorization({ isTelegram, loading, failed, onContinue }: { isTelegram: boolean; loading: boolean; failed: boolean; onContinue: () => void }) {
  const { t } = useI18n();
  const title = failed ? t("firstRun.authFailedTitle") : isTelegram ? t("firstRun.telegramTitle") : t("firstRun.telegramOutsideTitle");
  const subtitle = failed ? t("firstRun.authFailedSubtitle") : isTelegram ? t("firstRun.telegramSubtitle") : t("firstRun.telegramOutsideSubtitle");
  const action = failed || !isTelegram ? t("common.retry") : t("firstRun.continueTelegram");
  return <main className="first-run-screen">
    <div className="flex min-h-0 flex-1 flex-col">
    <div className="grid h-20 w-20 place-items-center rounded-[28px] bg-blue-50 text-brand-blue shadow-card"><Icon className="h-9 w-9" name="send" /></div>
    <div className="mt-7"><p className="text-sm font-bold text-brand-blue">{t("common.appName")}</p><h1 className="mt-3 text-3xl font-extrabold tracking-tight">{title}</h1><p className="mt-3 text-sm leading-6 text-brand-muted">{subtitle}</p></div>
    <Card className="mt-7 border-blue-100 bg-gradient-to-br from-blue-50 via-white to-cyan-50"><div className="flex gap-3"><Icon className="mt-0.5 text-brand-blue" name="lock" /><p className="text-sm leading-6 text-brand-muted">{t("firstRun.telegramPrivacy")}</p></div></Card>
    <Button aria-busy={loading} className="mt-auto w-full shrink-0" disabled={loading} onClick={onContinue} type="button"><Icon name="send" />{loading ? t("firstRun.authorizing") : action}</Button>
    </div>
  </main>;
}
