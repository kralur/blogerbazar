import { useRef, useState, type KeyboardEvent, type ReactNode } from "react";
import { botLink, type StartLabel } from "./bot";
import { useI18n, type Language } from "./i18n";
import { usePublicStats } from "./stats";
import { useTheme } from "./theme";

// The public landing page: what BloggerBazar is, how it works, and buttons that open the bot.
export function App() {
  return (
    <div className="min-h-screen bg-brand-bg text-brand-ink">
      <Header />
      <main>
        <Hero />
        <Numbers />
        <HowItWorks />
        <Why />
        <ForWhom />
        <Faq />
        <FinalCall />
      </main>
      <Footer />
    </div>
  );
}

const container = "mx-auto w-full max-w-6xl px-4 sm:px-6";

function OpenButton({ start, children, variant = "primary", className = "" }: { start: StartLabel; children: ReactNode; variant?: "primary" | "secondary"; className?: string }) {
  const look = variant === "primary"
    ? "bg-[var(--bb-accent)] text-[var(--bb-accent-text)] hover:bg-[var(--bb-accent-pressed)]"
    : "border border-brand-line bg-brand-surface text-brand-ink hover:bg-brand-soft";
  return (
    <a
      href={botLink(start)}
      target="_blank"
      rel="noopener noreferrer"
      className={`inline-flex h-12 items-center justify-center rounded-[var(--bb-radius-control)] px-6 text-base font-bold transition-colors ${look} ${className}`}
    >
      {children}
    </a>
  );
}

function Header() {
  const { t, language, setLanguage } = useI18n();
  const { dark, toggle } = useTheme();
  const languages: Language[] = ["ru", "uz"];
  return (
    <header className="sticky top-0 z-10 border-b border-brand-line bg-[color-mix(in_srgb,var(--bb-background)_90%,transparent)] backdrop-blur">
      <div className={`${container} flex h-16 items-center gap-2 sm:gap-3`}>
        <a href="#top" aria-label={t("nav.home")} className="text-base font-extrabold tracking-tight sm:text-lg">
          Blogger<span className="text-brand-blue">Bazar</span>
        </a>
        <nav className="ml-6 hidden gap-6 text-sm font-semibold text-brand-muted md:flex">
          <a className="hover:text-brand-ink" href="#how">{t("nav.how")}</a>
          <a className="hover:text-brand-ink" href="#why">{t("nav.why")}</a>
          <a className="hover:text-brand-ink" href="#faq">{t("nav.faq")}</a>
        </nav>
        <div className="ml-auto flex items-center gap-2">
          <div role="group" aria-label={t("lang.label")} className="flex rounded-[var(--bb-radius-control)] border border-brand-line p-0.5">
            {languages.map((value) => (
              <button
                key={value}
                type="button"
                aria-pressed={language === value}
                onClick={() => setLanguage(value)}
                className={`h-9 min-w-[2.5rem] rounded-[calc(var(--bb-radius-control)-2px)] px-2 text-sm font-bold ${language === value ? "bg-brand-ink text-brand-bg" : "text-brand-muted hover:text-brand-ink"}`}
              >
                {t(`lang.${value}`)}
              </button>
            ))}
          </div>
          <button
            type="button"
            onClick={toggle}
            aria-label={dark ? t("theme.toLight") : t("theme.toDark")}
            className="grid h-10 w-10 place-items-center rounded-[var(--bb-radius-control)] border border-brand-line text-brand-muted hover:text-brand-ink"
          >
            {dark ? <SunIcon /> : <MoonIcon />}
          </button>
          <OpenButton start="site" className="hidden h-10 px-4 text-sm sm:inline-flex">{t("nav.open")}</OpenButton>
        </div>
      </div>
    </header>
  );
}

function Hero() {
  const { t } = useI18n();
  return (
    <section id="top" className={`${container} grid items-center gap-10 py-12 sm:py-20 lg:grid-cols-[1.15fr_0.85fr] [&>*]:min-w-0`}>
      <div>
        <p className="inline-flex rounded-full bg-[var(--bb-accent)] px-3 py-1 text-sm font-bold text-[var(--bb-accent-text)]">{t("hero.badge")}</p>
        <h1 className="mt-5 break-words text-[2rem] font-extrabold leading-[1.05] tracking-tight min-[360px]:text-4xl sm:text-5xl lg:text-6xl">{t("hero.title")}</h1>
        <p className="mt-5 max-w-xl text-lg leading-8 text-brand-muted">{t("hero.subtitle")}</p>
        <div className="mt-8 flex flex-col gap-3 sm:flex-row">
          <OpenButton start="site_blogger">{t("hero.blogger")}</OpenButton>
          <OpenButton start="site_business" variant="secondary">{t("hero.business")}</OpenButton>
        </div>
        <p className="mt-4 text-sm text-brand-muted">{t("hero.note")}</p>
      </div>
      <PhoneMock />
    </section>
  );
}

// A drawn screen of the app (not a screenshot): a campaign, a blogger who answered, a finished deal.
function PhoneMock() {
  const { t } = useI18n();
  return (
    <figure aria-label={t("mock.label")} className="mx-auto w-full max-w-[20rem]">
      <div className="rounded-[2.25rem] border border-brand-line bg-brand-surface p-3 shadow-soft">
        <div className="rounded-[1.75rem] bg-brand-bg p-4">
          <div className="mx-auto mb-4 h-1.5 w-16 rounded-full bg-brand-line" />
          <div className="rounded-[var(--bb-radius-card)] bg-brand-surface p-4 shadow-card">
            <p className="text-xs font-semibold text-brand-muted">{t("mock.campaign")}</p>
            <p className="mt-1 font-extrabold leading-snug">{t("mock.campaignTitle")}</p>
            <p className="mt-2 text-sm text-brand-muted">{t("mock.budget")}</p>
            <p className="mt-3 inline-flex rounded-full bg-[var(--bb-action-subtle)] px-2.5 py-1 text-xs font-bold text-brand-blue">{t("mock.responses")}</p>
          </div>
          <div className="mt-3 flex items-center gap-3 rounded-[var(--bb-radius-card)] bg-brand-surface p-4 shadow-card">
            <span aria-hidden="true" className="grid h-11 w-11 shrink-0 place-items-center rounded-full bg-brand-blue text-lg font-extrabold text-brand-bg">{t("mock.bloggerName").slice(0, 1)}</span>
            <div className="min-w-0">
              <p className="font-bold">{t("mock.bloggerName")}</p>
              <p className="truncate text-xs text-brand-muted">{t("mock.bloggerMeta")}</p>
            </div>
          </div>
          <div className="mt-3 flex items-center justify-between gap-2 rounded-[var(--bb-radius-card)] bg-[var(--bb-accent)] p-4 text-[var(--bb-accent-text)]">
            <p className="text-sm font-extrabold">{t("mock.deal")}</p>
            <p className="shrink-0 text-sm font-bold">★ {t("mock.review")}</p>
          </div>
        </div>
      </div>
    </figure>
  );
}

// Real totals, shown as they are (owner, 2026-10-21); the block appears only once the numbers have arrived.
function Numbers() {
  const { t, language } = useI18n();
  const stats = usePublicStats();
  if (!stats) return null;
  const format = (value: number) => new Intl.NumberFormat(language === "uz" ? "uz-UZ" : "ru-RU").format(value);
  const items = [
    { key: "bloggers", value: stats.bloggers },
    { key: "businesses", value: stats.businesses },
    { key: "completedDeals", value: stats.completedDeals }
  ];
  return (
    <section aria-labelledby="numbers-title" className={`${container} pb-4`}>
      <h2 id="numbers-title" className="text-sm font-bold text-brand-muted">{t("numbers.title")}</h2>
      <dl className="mt-3 grid grid-cols-1 gap-3 min-[360px]:grid-cols-3">
        {items.map(({ key, value }) => (
          // The label comes first for screen readers (dt before dd); the number is shown on top.
          <div key={key} className="flex min-w-0 flex-col-reverse justify-end rounded-[var(--bb-radius-card)] border border-brand-line bg-brand-surface p-3 sm:p-6">
            <dt className="mt-1 text-xs leading-5 text-brand-muted sm:text-sm">{t(`numbers.${key}`, { count: value })}</dt>
            <dd className="break-words text-xl font-extrabold tracking-tight min-[360px]:text-2xl sm:text-4xl">{format(value)}</dd>
          </div>
        ))}
      </dl>
    </section>
  );
}

type Side = "business" | "blogger";
const sides: Side[] = ["business", "blogger"];

function HowItWorks() {
  const { t } = useI18n();
  const [side, setSide] = useState<Side>("business");
  const tabs = useRef<(HTMLButtonElement | null)[]>([]);
  // Arrow keys move between the two tabs (the WAI-ARIA tabs pattern).
  const onKeyDown = (event: KeyboardEvent<HTMLButtonElement>, index: number) => {
    if (event.key !== "ArrowRight" && event.key !== "ArrowLeft") return;
    event.preventDefault();
    const next = (index + (event.key === "ArrowRight" ? 1 : sides.length - 1)) % sides.length;
    setSide(sides[next]);
    tabs.current[next]?.focus();
  };
  return (
    <section id="how" className={`${container} py-12 sm:py-16`}>
      <h2 className="text-3xl font-extrabold tracking-tight sm:text-4xl">{t("how.title")}</h2>
      <div role="tablist" aria-label={t("how.tabs")} className="mt-6 inline-flex rounded-[var(--bb-radius-control)] bg-brand-soft p-1">
        {sides.map((value, index) => (
          <button
            key={value}
            ref={(element) => { tabs.current[index] = element; }}
            id={`how-tab-${value}`}
            type="button"
            role="tab"
            aria-selected={side === value}
            aria-controls="how-panel"
            tabIndex={side === value ? 0 : -1}
            onClick={() => setSide(value)}
            onKeyDown={(event) => onKeyDown(event, index)}
            className={`h-10 rounded-[calc(var(--bb-radius-control)-2px)] px-4 text-sm font-bold ${side === value ? "bg-brand-surface text-brand-ink shadow-card" : "text-brand-muted hover:text-brand-ink"}`}
          >
            {t(`how.tab.${value}`)}
          </button>
        ))}
      </div>
      <ol id="how-panel" role="tabpanel" aria-labelledby={`how-tab-${side}`} className="mt-6 grid gap-4 md:grid-cols-3">
        {[1, 2, 3].map((number) => (
          <li key={number} className="rounded-[var(--bb-radius-card)] border border-brand-line bg-brand-surface p-6">
            <p className="text-sm font-bold text-brand-blue">{t("how.step", { number })}</p>
            <h3 className="mt-2 text-xl font-extrabold">{t(`how.${side}.${number}.title`)}</h3>
            <p className="mt-2 leading-7 text-brand-muted">{t(`how.${side}.${number}.text`)}</p>
          </li>
        ))}
      </ol>
    </section>
  );
}

const reasons = [
  { key: "moderation", icon: <ShieldIcon /> },
  { key: "direct", icon: <HandshakeIcon /> },
  { key: "reviews", icon: <StarIcon /> },
  { key: "telegram", icon: <PlaneIcon /> }
];

function Why() {
  const { t } = useI18n();
  return (
    <section id="why" className="bg-brand-surface py-12 sm:py-16">
      <div className={container}>
        <h2 className="text-3xl font-extrabold tracking-tight sm:text-4xl">{t("why.title")}</h2>
        <ul className="mt-8 grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          {reasons.map(({ key, icon }) => (
            <li key={key} className="rounded-[var(--bb-radius-card)] bg-brand-bg p-6">
              <span aria-hidden="true" className="grid h-11 w-11 place-items-center rounded-[var(--bb-radius-control)] bg-[var(--bb-action-subtle)] text-brand-blue">{icon}</span>
              <h3 className="mt-4 text-lg font-extrabold">{t(`why.${key}.title`)}</h3>
              <p className="mt-2 leading-7 text-brand-muted">{t(`why.${key}.text`)}</p>
            </li>
          ))}
        </ul>
      </div>
    </section>
  );
}

const audiences: { key: string; start: StartLabel }[] = [
  { key: "blogger", start: "site_blogger" },
  { key: "brandFace", start: "site_blogger" },
  { key: "business", start: "site_business" }
];

function ForWhom() {
  const { t } = useI18n();
  return (
    <section className={`${container} py-12 sm:py-16`}>
      <h2 className="text-3xl font-extrabold tracking-tight sm:text-4xl">{t("who.title")}</h2>
      <ul className="mt-8 grid gap-4 md:grid-cols-3">
        {audiences.map(({ key, start }) => (
          <li key={key} className="flex flex-col rounded-[var(--bb-radius-card)] border border-brand-line bg-brand-surface p-6">
            <h3 className="text-xl font-extrabold">{t(`who.${key}.title`)}</h3>
            <p className="mt-2 flex-1 leading-7 text-brand-muted">{t(`who.${key}.text`)}</p>
            <a href={botLink(start)} target="_blank" rel="noopener noreferrer" className="mt-4 font-bold text-brand-blue underline-offset-4 hover:underline">
              {t("nav.open")} →
            </a>
          </li>
        ))}
      </ul>
    </section>
  );
}

const questions = ["price", "check", "brandFace", "contact", "language", "problem"];

function Faq() {
  const { t } = useI18n();
  return (
    <section id="faq" className="bg-brand-surface py-12 sm:py-16">
      <div className={`${container} max-w-3xl`}>
        <h2 className="text-3xl font-extrabold tracking-tight sm:text-4xl">{t("faq.title")}</h2>
        <div className="mt-8 divide-y divide-brand-line border-y border-brand-line">
          {questions.map((key) => (
            <details key={key} className="group py-1">
              <summary className="flex cursor-pointer list-none items-center justify-between gap-4 py-4 text-lg font-bold">
                {t(`faq.${key}.question`)}
                <span aria-hidden="true" className="text-2xl leading-none text-brand-muted transition-transform group-open:rotate-45">+</span>
              </summary>
              <p className="pb-5 leading-7 text-brand-muted">{t(`faq.${key}.answer`)}</p>
            </details>
          ))}
        </div>
      </div>
    </section>
  );
}

function FinalCall() {
  const { t } = useI18n();
  return (
    <section className={`${container} py-12 sm:py-20`}>
      <div className="rounded-[var(--bb-radius-overlay)] bg-brand-ink px-6 py-12 text-center text-brand-bg sm:px-12">
        <h2 className="text-3xl font-extrabold tracking-tight sm:text-4xl">{t("cta.title")}</h2>
        <p className="mx-auto mt-3 max-w-xl text-lg opacity-80">{t("cta.text")}</p>
        <OpenButton start="site" className="mt-8">{t("nav.open")}</OpenButton>
      </div>
    </section>
  );
}

function Footer() {
  const { t } = useI18n();
  return (
    <footer className="border-t border-brand-line">
      <div className={`${container} flex flex-col gap-2 py-8 text-sm text-brand-muted sm:flex-row sm:items-center sm:justify-between`}>
        <p>{t("footer.rights", { year: new Date().getFullYear() })}</p>
        <a href={botLink("site")} target="_blank" rel="noopener noreferrer" className="font-semibold hover:text-brand-ink">{t("footer.bot")}</a>
      </div>
    </footer>
  );
}

const iconProps = { width: 22, height: 22, viewBox: "0 0 24 24", fill: "none", stroke: "currentColor", strokeWidth: 2, strokeLinecap: "round" as const, strokeLinejoin: "round" as const };
function ShieldIcon() { return <svg {...iconProps}><path d="M12 3l8 3v6c0 4.5-3.4 8.3-8 9-4.6-.7-8-4.5-8-9V6l8-3z" /><path d="M9 12l2 2 4-4" /></svg>; }
function HandshakeIcon() { return <svg {...iconProps}><path d="M3 12l4-4 4 3 3-3 7 6" /><path d="M7 14l3 3a2 2 0 003 0l5-5" /></svg>; }
function StarIcon() { return <svg {...iconProps}><path d="M12 3l2.7 5.6 6.1.9-4.4 4.3 1 6.1L12 17l-5.4 2.9 1-6.1-4.4-4.3 6.1-.9L12 3z" /></svg>; }
function PlaneIcon() { return <svg {...iconProps}><path d="M21 4L3 11l6 2 2 6 3-4 4 3 3-14z" /><path d="M9 13l12-9" /></svg>; }
function MoonIcon() { return <svg {...iconProps} width={18} height={18}><path d="M20 14.5A8 8 0 019.5 4 8 8 0 1020 14.5z" /></svg>; }
function SunIcon() { return <svg {...iconProps} width={18} height={18}><circle cx="12" cy="12" r="4" /><path d="M12 2v2M12 20v2M4.9 4.9l1.4 1.4M17.7 17.7l1.4 1.4M2 12h2M20 12h2M4.9 19.1l1.4-1.4M17.7 6.3l1.4-1.4" /></svg>; }
