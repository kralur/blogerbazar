import { useRef, useState, type KeyboardEvent, type ReactNode } from "react";
import { botLink, type StartLabel } from "./bot";
import { useI18n, type Language } from "./i18n";
import { usePublicStats } from "./stats";
import { useTheme } from "./theme";

// The public landing page: what BloggerBazar is, how it works, and buttons that open the bot.
// Direction A "Bazar" (owner, 2026-11-07): a lapis first screen with offers hung as price tags, an ikat strip, then a
// quiet page. Lime is kept for the action that opens the bot.
export function App() {
  return (
    <div className="min-h-screen bg-[var(--l-page)] text-[var(--l-text)]">
      <Header />
      <main>
        <Hero />
        <Ikat />
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
const sectionTitle = "font-display text-[1.65rem] font-bold leading-tight tracking-[-0.02em] sm:text-4xl";

function OpenButton({ start, children, variant = "primary", className = "" }: { start: StartLabel; children: ReactNode; variant?: "primary" | "outline"; className?: string }) {
  const look = variant === "primary"
    ? "bg-[var(--l-lime)] text-[var(--l-on-lime)] hover:bg-[var(--l-lime-pressed)]"
    : "text-white shadow-[inset_0_0_0_2px_rgba(255,255,255,.55)] hover:shadow-[inset_0_0_0_2px_#fff]";
  return (
    <a
      href={botLink(start)}
      target="_blank"
      rel="noopener noreferrer"
      className={`inline-flex h-[3.25rem] items-center justify-center rounded-[14px] px-6 text-[1.0625rem] font-semibold transition-[background-color,box-shadow] ${look} ${className}`}
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
    <header className="on-lapis sticky top-0 z-10 bg-[var(--l-lapis)] text-white">
      <div className={`${container} flex h-16 items-center gap-2 sm:gap-3`}>
        <a href="#top" aria-label={t("nav.home")} className="font-display text-base font-bold tracking-[-0.01em] sm:text-lg">
          Blogger<span className="text-[var(--l-lime)]">Bazar</span>
        </a>
        <nav className="ml-8 hidden gap-6 text-sm font-medium text-[var(--l-lapis-text)] md:flex">
          <a className="hover:text-white" href="#how">{t("nav.how")}</a>
          <a className="hover:text-white" href="#why">{t("nav.why")}</a>
          <a className="hover:text-white" href="#faq">{t("nav.faq")}</a>
        </nav>
        <div className="ml-auto flex items-center gap-2">
          <div role="group" aria-label={t("lang.label")} className="flex gap-0.5">
            {languages.map((value) => (
              <button
                key={value}
                type="button"
                aria-pressed={language === value}
                onClick={() => setLanguage(value)}
                className={`h-9 min-w-[2.75rem] rounded-full px-2.5 text-sm font-semibold ${language === value ? "bg-white text-[var(--l-lapis)]" : "text-[var(--l-lapis-text)] hover:text-white"}`}
              >
                {t(`lang.${value}`)}
              </button>
            ))}
          </div>
          <button
            type="button"
            onClick={toggle}
            aria-label={dark ? t("theme.toLight") : t("theme.toDark")}
            className="grid h-10 w-10 place-items-center rounded-full text-[var(--l-lapis-text)] hover:text-white"
          >
            {dark ? <SunIcon /> : <MoonIcon />}
          </button>
          <OpenButton start="site" className="hidden !h-10 px-4 text-sm sm:inline-flex">{t("nav.open")}</OpenButton>
        </div>
      </div>
    </header>
  );
}

function Hero() {
  const { t } = useI18n();
  return (
    <section id="top" className="on-lapis bg-[var(--l-lapis)] text-white">
      <div className={`${container} grid items-center gap-10 pb-14 pt-10 sm:pb-20 sm:pt-16 lg:grid-cols-[1.05fr_0.95fr] [&>*]:min-w-0`}>
        <div>
          <h1 className="font-display max-w-[14ch] text-[1.8rem] font-extrabold leading-[1.04] tracking-[-0.03em] [hyphens:manual] min-[360px]:text-[2.1rem] min-[400px]:text-[2.3rem] sm:text-6xl lg:text-[3.6rem] xl:text-[3.9rem]">{t("hero.title")}</h1>
          <p className="mt-6 max-w-[44ch] text-lg leading-[1.55] text-[var(--l-lapis-text)]">{t("hero.subtitle")}</p>
          <div className="mt-8 flex flex-col gap-3 min-[400px]:flex-row">
            <OpenButton start="site_business">{t("hero.business")}</OpenButton>
            <OpenButton start="site_blogger" variant="outline">{t("hero.blogger")}</OpenButton>
          </div>
          <p className="mt-4 text-sm text-[var(--l-lapis-muted)]">{t("hero.note")}</p>
        </div>
        <PriceTags />
      </div>
    </section>
  );
}

// Example offers, drawn (not real listings): what a business sees in the app, priced in sum like a bazaar stall.
const tags = [
  { key: "1", price: 1_500_000, place: "lg:left-0 lg:top-[34px] lg:-rotate-2" },
  { key: "2", price: 3_200_000, place: "self-end lg:left-[250px] lg:top-[118px] lg:rotate-[1.5deg]" },
  { key: "3", price: 800_000, place: "lg:left-6 lg:top-[284px] lg:-rotate-1" }
];

function PriceTags() {
  const { t } = useI18n();
  // Prices are grouped with spaces in both languages, as on price tags in Uzbekistan ("1 500 000 so‘m").
  const format = (value: number) => new Intl.NumberFormat("ru-RU").format(value);
  return (
    <figure aria-labelledby="tags-caption" className="relative mx-auto w-full max-w-[33rem]">
      <div aria-hidden="true" className="absolute left-[8%] right-[4%] top-0 hidden h-16 rounded-[50%] border-t-2 border-white/35 lg:block" />
      <ul className="flex flex-col gap-3.5 pt-2 lg:block lg:h-[29rem] lg:pt-0">
        {tags.map(({ key, price, place }) => (
          <li key={key} className={`price-tag w-[min(18.5rem,88vw)] lg:absolute lg:w-[17.5rem] ${place}`}>
            <p className="text-[0.8125rem] text-[var(--l-tag-muted)]">{t(`tag.${key}.who`)}</p>
            <p className="mt-1 font-semibold leading-snug">{t(`tag.${key}.what`)}</p>
            <p className="font-display mt-3 whitespace-nowrap text-2xl font-bold tracking-[-0.02em]">
              {format(price)}<span className="ml-1.5 font-sans text-sm font-semibold">{t("tag.currency")}</span>
            </p>
            <p className="mt-2.5 flex flex-wrap gap-1.5 text-xs font-semibold">
              <span className="rounded-full bg-[#E8EDF7] px-2 py-1">{t(`tag.${key}.category`)}</span>
              <span className="rounded-full bg-[#D9F3F1] px-2 py-1 text-[#0D6B65]">{t(`tag.${key}.responses`)}</span>
            </p>
          </li>
        ))}
      </ul>
      <figcaption id="tags-caption" className="mt-4 text-sm text-[var(--l-lapis-muted)] lg:mt-0">{t("tag.caption")}</figcaption>
    </figure>
  );
}

// Three flames (saffron, lime, coral) with a three-layer core, thin threads between them; the feathering is light
// so the motif stays crisp. The two strips on the page share these ids, which is fine: the defs are identical.
function Ikat() {
  const flame = (x: number, outer: string, middle: string, core: string) => <>
    <path d={`M${x} 0 C${x + 12} 10 ${x + 16} 20 ${x + 16} 24 C${x + 16} 28 ${x + 12} 38 ${x} 48 C${x - 12} 38 ${x - 16} 28 ${x - 16} 24 C${x - 16} 20 ${x - 12} 10 ${x} 0 Z`} fill={outer} />
    <path d={`M${x} 8 C${x + 7} 15 ${x + 9} 20 ${x + 9} 24 C${x + 9} 28 ${x + 7} 33 ${x} 40 C${x - 7} 33 ${x - 9} 28 ${x - 9} 24 C${x - 9} 20 ${x - 7} 15 ${x} 8 Z`} fill={middle} />
    <path d={`M${x} 17 C${x + 3} 20 ${x + 4} 22 ${x + 4} 24 C${x + 4} 26 ${x + 3} 28 ${x} 31 C${x - 3} 28 ${x - 4} 26 ${x - 4} 24 C${x - 4} 22 ${x - 3} 20 ${x} 17 Z`} fill={core} />
  </>;
  return (
    <div aria-hidden="true" className="ikat">
      <svg preserveAspectRatio="none">
        <defs>
          <filter id="ikat-feather" x="-5%" y="-10%" width="110%" height="120%">
            <feTurbulence type="fractalNoise" baseFrequency=".015 .7" numOctaves={3} seed={7} result="noise" />
            <feDisplacementMap in="SourceGraphic" in2="noise" scale={5} xChannelSelector="R" yChannelSelector="G" />
          </filter>
          {/* The motif is drawn at 176x48 and shown at 110x30 (owner, 2026-11-07: the strip was too big). */}
          <pattern id="ikat-motif" width="110" height="30" patternUnits="userSpaceOnUse">
            <g transform="scale(.625)">
              <rect width="176" height="48" fill="var(--l-lapis)" />
              {flame(28, "var(--l-saffron)", "var(--l-ink)", "#FFFFFF")}
              {flame(88, "var(--l-lime)", "var(--l-tile)", "var(--l-ink)")}
              {flame(148, "var(--l-coral)", "#FFFFFF", "var(--l-lapis)")}
              <rect x="2" width="3" height="48" fill="var(--l-tile)" />
              <rect x="57" width="3" height="48" fill="var(--l-lime)" />
              <rect x="117" width="3" height="48" fill="var(--l-saffron)" />
            </g>
          </pattern>
        </defs>
        <rect width="100%" height="30" fill="url(#ikat-motif)" filter="url(#ikat-feather)" />
      </svg>
    </div>
  );
}

// Real totals (owner, 2026-10-21), but only once they look like a living marketplace: "5 bloggers" tells a visitor
// from an ad that nobody is here (owner, 2026-11-07). Below the threshold the page simply has no numbers block.
export const MIN_PUBLIC_BLOGGERS = 100;

function Numbers() {
  const { t, language } = useI18n();
  const stats = usePublicStats();
  if (!stats || stats.bloggers < MIN_PUBLIC_BLOGGERS) return null;
  const format = (value: number) => new Intl.NumberFormat(language === "uz" ? "uz-UZ" : "ru-RU").format(value);
  const items = [
    { key: "bloggers", value: stats.bloggers },
    { key: "businesses", value: stats.businesses },
    { key: "completedDeals", value: stats.completedDeals }
  ];
  return (
    <section aria-labelledby="numbers-title" className={`${container} pt-12 sm:pt-16`}>
      <h2 id="numbers-title" className="text-sm font-semibold text-[var(--l-muted)]">{t("numbers.title")}</h2>
      <dl className="mt-3 grid grid-cols-1 gap-6 min-[360px]:grid-cols-3">
        {items.map(({ key, value }) => (
          // The label comes first for screen readers (dt before dd); the number is shown on top.
          <div key={key} className="flex min-w-0 flex-col-reverse justify-end border-t-[3px] border-[var(--l-text)] pt-3">
            <dt className="mt-1 text-sm text-[var(--l-muted)]">{t(`numbers.${key}`, { count: value })}</dt>
            <dd className="font-display break-words text-2xl font-extrabold tracking-[-0.02em] sm:text-5xl">{format(value)}</dd>
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
    <section id="how" className={`${container} py-14 sm:py-20`}>
      <div className="flex flex-col gap-5 sm:flex-row sm:items-end sm:justify-between">
        <h2 className={sectionTitle}>{t("how.title")}</h2>
        <div role="tablist" aria-label={t("how.tabs")} className="inline-flex self-start rounded-full bg-[var(--l-quiet)] p-1 sm:self-auto">
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
              className={`h-10 rounded-full px-4 text-sm font-semibold ${side === value ? "bg-[var(--l-lapis)] text-white" : "text-[var(--l-muted)] hover:text-[var(--l-text)]"}`}
            >
              {t(`how.tab.${value}`)}
            </button>
          ))}
        </div>
      </div>
      {/* A real sequence, so the steps carry numbers. */}
      <ol id="how-panel" role="tabpanel" aria-labelledby={`how-tab-${side}`} className="mt-10 grid gap-8 md:grid-cols-3 md:gap-10">
        {[1, 2, 3].map((number) => (
          <li key={number} className="border-t-[3px] border-[var(--l-text)] pt-4">
            <p aria-hidden="true" className="font-display text-[2.75rem] font-extrabold leading-none text-[var(--l-numeral)]">{number}</p>
            <h3 className="mt-3 text-[1.1875rem] font-semibold">
              <span className="sr-only">{t("how.step", { number })}. </span>
              {t(`how.${side}.${number}.title`)}
            </h3>
            <p className="mt-2 max-w-[34ch] leading-[1.6] text-[var(--l-muted)]">{t(`how.${side}.${number}.text`)}</p>
          </li>
        ))}
      </ol>
    </section>
  );
}

const reasons = ["moderation", "direct", "reviews", "telegram"];

function Why() {
  const { t } = useI18n();
  return (
    <section id="why" className="bg-[var(--l-quiet)] py-14 sm:py-20">
      <div className={`${container} grid gap-8 lg:grid-cols-[0.8fr_1.2fr] lg:gap-16`}>
        <h2 className={sectionTitle}>{t("why.title")}</h2>
        <dl className="divide-y divide-[var(--l-line)] border-y border-[var(--l-line)]">
          {reasons.map((key) => (
            <div key={key} className="grid gap-1 py-5 sm:grid-cols-[14rem_1fr] sm:gap-6">
              <dt className="text-lg font-semibold">{t(`why.${key}.title`)}</dt>
              <dd className="leading-[1.6] text-[var(--l-muted)]">{t(`why.${key}.text`)}</dd>
            </div>
          ))}
        </dl>
      </div>
    </section>
  );
}

const audiences: { key: string; start: StartLabel }[] = [
  { key: "business", start: "site_business" },
  { key: "blogger", start: "site_blogger" },
  { key: "brandFace", start: "site_blogger" }
];

function ForWhom() {
  const { t } = useI18n();
  return (
    <section className={`${container} py-14 sm:py-20`}>
      <h2 className={sectionTitle}>{t("who.title")}</h2>
      <ul className="mt-8 border-t border-[var(--l-line)]">
        {audiences.map(({ key, start }) => (
          <li key={key} className="grid gap-2 border-b border-[var(--l-line)] py-6 md:grid-cols-[14rem_1fr_auto] md:items-center md:gap-8">
            <h3 className="font-display text-xl font-bold tracking-[-0.01em]">{t(`who.${key}.title`)}</h3>
            <p className="max-w-[52ch] leading-[1.6] text-[var(--l-muted)]">{t(`who.${key}.text`)}</p>
            <a href={botLink(start)} target="_blank" rel="noopener noreferrer" className="font-semibold text-[var(--l-text)] underline decoration-[var(--l-tile)] decoration-2 underline-offset-4 hover:decoration-[var(--l-text)]">
              {t("nav.open")}
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
    <section id="faq" className="bg-[var(--l-quiet)] py-14 sm:py-20">
      <div className={`${container} max-w-3xl`}>
        <h2 className={sectionTitle}>{t("faq.title")}</h2>
        <div className="mt-8 divide-y divide-[var(--l-line)] border-y border-[var(--l-line)]">
          {questions.map((key) => (
            <details key={key} className="group py-1">
              <summary className="flex cursor-pointer list-none items-center justify-between gap-4 py-4 text-lg font-semibold">
                {t(`faq.${key}.question`)}
                <span aria-hidden="true" className="text-2xl leading-none text-[var(--l-muted)] transition-transform group-open:rotate-45 motion-reduce:transition-none">+</span>
              </summary>
              <p className="max-w-[62ch] pb-5 leading-[1.6] text-[var(--l-muted)]">{t(`faq.${key}.answer`)}</p>
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
    <section className="on-lapis bg-[var(--l-lapis)] text-white">
      <Ikat />
      <div className={`${container} py-14 sm:py-20`}>
        <h2 className="font-display max-w-[16ch] text-[1.9rem] font-extrabold leading-[1.05] tracking-[-0.03em] sm:text-5xl">{t("cta.title")}</h2>
        <p className="mt-4 max-w-[44ch] text-lg text-[var(--l-lapis-text)]">{t("cta.text")}</p>
        <OpenButton start="site" className="mt-8">{t("nav.open")}</OpenButton>
      </div>
    </section>
  );
}

function Footer() {
  const { t } = useI18n();
  return (
    <footer className="bg-[var(--l-ink)] text-[var(--l-lapis-muted)]">
      <div className={`${container} flex flex-col gap-2 py-8 text-sm sm:flex-row sm:items-center sm:justify-between`}>
        <p>{t("footer.rights", { year: new Date().getFullYear() })}</p>
        <a href={botLink("site")} target="_blank" rel="noopener noreferrer" className="font-semibold hover:text-white">{t("footer.bot")}</a>
      </div>
    </footer>
  );
}

const iconProps = { width: 22, height: 22, viewBox: "0 0 24 24", fill: "none", stroke: "currentColor", strokeWidth: 2, strokeLinecap: "round" as const, strokeLinejoin: "round" as const };
function MoonIcon() { return <svg {...iconProps} width={18} height={18}><path d="M20 14.5A8 8 0 019.5 4 8 8 0 1020 14.5z" /></svg>; }
function SunIcon() { return <svg {...iconProps} width={18} height={18}><circle cx="12" cy="12" r="4" /><path d="M12 2v2M12 20v2M4.9 4.9l1.4 1.4M17.7 17.7l1.4 1.4M2 12h2M20 12h2M4.9 19.1l1.4-1.4M17.7 6.3l1.4-1.4" /></svg>; }
