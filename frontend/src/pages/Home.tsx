import { useI18n } from "../i18n";
import { BottomNav, Button, Card, Icon, PaywallCard, StatsCard } from "../components/ui";

export function Home() {
  const { language, setLanguage, t } = useI18n();
  const toggleLanguage = () => setLanguage(language === "ru" ? "uz" : "ru");

  return <div className="screen pb-28">
    <header className="flex items-center justify-between pt-2"><p className="text-sm font-semibold text-brand-muted">{t("home.welcome")}</p><button aria-label={t("language.switch")} className="grid h-11 min-w-11 place-items-center rounded-full bg-white px-3 text-xs font-extrabold text-brand-blue shadow-card" onClick={toggleLanguage} type="button">{language.toUpperCase()}</button></header>
    <section className="mt-4"><h1 className="max-w-[280px] text-[34px] font-extrabold leading-[1.06] tracking-[-0.04em]">{t("home.title")}</h1><p className="mt-3 max-w-[280px] text-[15px] leading-5 text-brand-muted">{t("home.subtitle")}</p></section>
    <div className="relative mt-5 h-56 overflow-hidden rounded-[32px] bg-gradient-to-br from-blue-100 via-white to-cyan-100"><div className="absolute -left-8 top-8 h-40 w-40 rounded-full bg-blue-300/30 blur-2xl" /><div className="absolute right-0 top-0 h-40 w-40 rounded-full bg-cyan-300/30 blur-2xl" /><div className="absolute bottom-6 left-7 grid h-24 w-24 place-items-center rounded-full border-4 border-white bg-gradient-to-br from-rose-100 to-orange-200 text-4xl shadow-card">👩🏻</div><div className="absolute bottom-6 right-7 grid h-24 w-24 place-items-center rounded-full border-4 border-white bg-gradient-to-br from-sky-100 to-blue-300 text-4xl shadow-card">👨🏻</div><span className="absolute bottom-13 left-1/2 grid h-12 w-12 -translate-x-1/2 place-items-center rounded-2xl bg-brand-gradient text-white shadow-glow"><Icon name="check" /></span><span className="absolute left-7 top-8 grid h-10 w-10 place-items-center rounded-2xl bg-white/80 text-brand-premium shadow-card"><Icon name="heart" /></span><span className="absolute right-8 top-11 grid h-10 w-10 place-items-center rounded-2xl bg-white/80 text-brand-blue shadow-card"><Icon name="chart" /></span></div>
    <div className="mt-4 grid gap-3"><a href="#/search"><Button className="w-full"><Icon name="search" />{t("home.iNeedBlogger")}</Button></a><a href="#/profile"><Button className="w-full" variant="secondary"><Icon name="users" />{t("home.iAmBlogger")}</Button></a><a href="#/business"><Button className="w-full" variant="secondary"><Icon name="building" />{t("home.iAmBusiness")}</Button></a></div>
    <div className="mt-5 grid grid-cols-3 gap-2"><StatsCard icon="users" label={t("home.bloggers")} value="5600+" /><StatsCard icon="building" label={t("home.companies")} value="740" /><StatsCard icon="star" label={t("home.rating")} value="4.9" /></div>
    <div className="mt-5"><PaywallCard cta={t("home.proCta")} subtitle={t("home.proSubtitle")} title={t("home.proTitle")} /></div>
    <Card className="mt-5"><div className="flex gap-3"><span className="grid h-11 w-11 shrink-0 place-items-center rounded-2xl bg-green-50 text-brand-success"><Icon name="check" /></span><div><h2 className="font-extrabold">{t("home.safeTitle")}</h2><p className="mt-1 text-sm leading-5 text-brand-muted">{t("home.safeDescription")}</p></div></div></Card><BottomNav />
  </div>;
}
